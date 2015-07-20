﻿/* DustFX is a derivative work based on CollisionFX by Pizzaoverhead.
 * Mofications have been made to stip out the spark system and strengthen the options
 * in customization for the dust effects purely for the use in wheels.  Previously,
 * A wheel would spark and dust whenever it rolled anywhere.  Now there is only dust, which
 * would be expected when rolling over a surface at the speeds we do in KSP.
 * Much of the config node system would have been impossible without the help of
 * xEvilReeperx who was very patient with my complete ignorance, and whom I may
 * return to in the future to further optimize the module as a whole.
 * 
 * Best used with Kerbal Foundries by lo-fi.  Reinventing the Wheel, quite literally!
 * Much thanks to xEvilReeperx for fixing the things I broke, without whom there would be no
 * config-node settings file, nor would I be sane enough to live alone... ever!
 * 
 * The end state of this code has nearly been rewritten completely and integrated into the mod
 * that it was meant to be used with: Kerbal Foundries.
 */

using System;
using UnityEngine;
using KerbalFoundries;

namespace KerbalFoundries
{
	/// <summary>DustFX class which is based on, but heavily modified from, CollisionFX by pizzaoverload.</summary>
	public class KFDustFX : PartModule
	{
		// Class-wide disabled warnings in SharpDevelop
		// disable AccessToStaticMemberViaDerivedType
		// disable RedundantDefaultFieldInitializer
		
		readonly KFLogUtil KFLog = new KFLogUtil();
		
		/// <summary>Part instance of KFModuleWheel</summary>
		KFModuleWheel _KFModuleWheel;
		
		/// <summary>The camera object we're using to get color info directly from the terrain.</summary>
		ModuleCameraShot _ModuleCameraShot;
		
		/// <summary>Local copy of the tweakScaleCorrector parameter in the KFModuleWheel module.</summary>
		public float tweakScaleCorrector;
		
		/// <summary>Mostly unnecessary, since there is no other purpose to having the module active.</summary>
		/// <remarks>Default is "true"</remarks>
		[KSPField]
		public bool dustEffects = true;
		
		/// <summary>Minimum scrape speed.</summary>
		/// <remarks>Default is 0.5.  Repulsors should have this extremely low.</remarks>
		[KSPField]
		public float minScrapeSpeed = 0.5f;
		
		/// <summary>Minimum dust energy value.</summary>
		/// <remarks>Default is 0.1.  Represents the minimum thickness of the particles.</remarks>
		[KSPField]
		public float minDustEnergy = 0.1f;
		
		/// <summary>Minimum dust energy value.</summary>
		/// <remarks>Default is 1.  Represents the maximum thickness of the particles.</remarks>
		[KSPField]
		public float maxDustEnergy = 1f;
		
		/// <summary>Minimum emission value of the dust particles.</summary>
		/// <remarks>Default is 0.1.  This is the number of particles to emit per second.</remarks>
		[KSPField]
		public float minDustEmission = 0.1f;
		
		/// <summary>Maximum emission value of the dust particles.</summary>
		/// <remarks>Default is 20.  This is the number of particles to emit per second.</remarks>
		[KSPField]
		public float maxDustEmission = 20f;
		
		/// <summary>Minimum size value of the dust particles.</summary>
		/// <remarks>Default is 0.1.  Represents the size of the particles themselves.</remarks>
		[KSPField]
		public float minDustSize = 0.1f;
		
		/// <summary>Maximum size value of the dust particles.</summary>
		/// <remarks>Default is 2.  Represents the size of the particles themselves.</remarks>
		[KSPField]
		public float maxDustSize = 2f;
		
		/// <summary>Maximum emission energy divisor.</summary>
		/// <remarks>Default is 2.  Divides the thickness by the value provided.</remarks>
		[KSPField]
		public float maxDustEnergyDiv = 2f;
		
		/// <summary>Maximum emission multiplier.</summary>
		/// <remarks>Default is 2.</remarks>
		[KSPField]
		public float maxDustEmissionMult = 2f;
		
		/// <summary>Used in the OnCollisionEnter/Stay methods to define the minimum velocity magnitude to check against.</summary>
		/// <remarks>Default is 2.  Would set very low for repulsors.</remarks>
		[KSPField]
		public float minVelocityMag = 2f;
		
		/// <summary>KSP path to the effect being used here.  Made into a field so that it can be customized in the future.</summary>
		/// <remarks>Default is "Effects/fx_smokeTrail_light"</remarks>
		[KSPField]
		public const string dustEffectObject = "Effects/fx_smokeTrail_light";
		
		/// <summary>Prefix the logs with this to identify it.</summary>
		public string strClassName = "KFDustFX";
		
		bool isPaused;
		GameObject kfdustFx;
		ParticleAnimator dustAnimator;
		Color colorDust;
		Color colorBiome;
		Color colorAverage;
		Color colorCam;
		
		/// <summary>Loaded from the KFConfigManager class.</summary>
		/// <remarks>Persistent field.</remarks>
		[Persistent]
		public bool isDustEnabledGlobally = true;
		
		/// <summary>Local dust disabler.</summary>
		/// <remarks>Is Persistent and active in all appropriate scenes by default.</remarks>
		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Dust Effects"), UI_Toggle(disabledText = "Disabled", enabledText = "Enabled")]
		public bool isDustEnabledLocally = true;
		
		public bool isDustCameraEnabled;
		
		/// <summary>CollisionInfo class for the DustFX module.</summary>
		public class CollisionInfo
		{
			public KFDustFX KFDustFX;
			public CollisionInfo(KFDustFX kfdustFX)
			{
				KFDustFX = kfdustFX;
			}
		}
		
		/// <summary>Part Info that will be displayed when part details are shown.</summary>
		/// <remarks>Can be overridden in the module config on a per-part basis.</remarks>
		[KSPField]
		public string strPartInfo = "This part will throw up dust when rolling over the terrain.";
		
		// Basic override for the info class.  Already has its own XML documentation.
		public override string GetInfo()
		{
			return strPartInfo;
		}
		
		public override void OnStart(StartState state)
		{
			_KFModuleWheel = this.part.GetComponent<KFModuleWheel>();
			tweakScaleCorrector = _KFModuleWheel.tweakScaleCorrector;
			
			isDustEnabledGlobally = KFPersistenceManager.isDustEnabled;
			isDustCameraEnabled = KFPersistenceManager.isDustCameraEnabled;
			
			if (!isDustEnabledGlobally && isDustEnabledLocally)
			{
				isDustEnabledLocally = isDustEnabledGlobally;
				Fields["isDustEnabledLocally"].guiActive = false;
				Fields["isDustEnabledLocally"].guiActiveEditor = false;
				return;
			}
			
			if (isDustEnabledGlobally && !isDustEnabledLocally)
				return;
			
			if (HighLogic.LoadedSceneIsFlight)
			{
				_ModuleCameraShot = vessel.GetComponent<ModuleCameraShot>();
				if (dustEffects)
					SetupParticles();
			}

			GameEvents.onGamePause.Add(OnPause);
			GameEvents.onGameUnpause.Add(OnUnpause);
		}
		
		/// <summary>Defines the particle effects used in this module.</summary>
		public void SetupParticles()
		{
			if (!dustEffects)
				return;
			kfdustFx = (GameObject)GameObject.Instantiate(Resources.Load(dustEffectObject));
			kfdustFx.transform.parent = part.transform;
			kfdustFx.transform.position = part.transform.position;
			kfdustFx.particleEmitter.localVelocity = Vector3.zero;
			kfdustFx.particleEmitter.useWorldSpace = true;
			kfdustFx.particleEmitter.emit = false;
			kfdustFx.particleEmitter.minEnergy = minDustEnergy;
			kfdustFx.particleEmitter.minEmission = minDustEmission;
			kfdustFx.particleEmitter.minSize = minDustSize;
			dustAnimator = kfdustFx.particleEmitter.GetComponent<ParticleAnimator>();
		}
		
		/// <summary>Contains information about what to do when the part stays in the collided state over a period of time.</summary>
		/// <param name="position">The position of the collision.</param>
		/// <param name="col">The collider being referenced.</param>
		public void CollisionScrape(Vector3 position, Collider col)
		{
			if (isPaused)
				return;
			Scrape(position, col);
		}
		
		/// <summary>Called when the part is scraping over a surface.</summary>
		/// <param name="col">The collider being referenced.</param>
		/// <param name="position">The position of the scape.</param>
		public void Scrape(Vector3 position, Collider col)
		{
			if ((isPaused || Equals(part, null)) || Equals(part.rigidbody, null))
				return;
			float fMagnitude = this.part.rigidbody.velocity.magnitude;
			DustParticles(fMagnitude, position + (part.rigidbody.velocity * Time.deltaTime), col);
		}
		
		/// <summary>This creates and maintains the dust particles and their body/biome specific colors.</summary>
		/// <param name="speed">Speed of the part which is scraping.</param>
		/// <param name="contactPoint">The point at which the collider and the scraped surface make contact.</param>
		/// <param name="col">The collider being referenced.</param>
		public void DustParticles(float speed, Vector3 contactPoint, Collider col)
		{
			if (!dustEffects || speed < minScrapeSpeed || Equals(dustAnimator, null) || !KFPersistenceManager.isDustEnabled)
				return;
			if (Equals(tweakScaleCorrector, 0) || tweakScaleCorrector < 0)
				tweakScaleCorrector = 1f;
			colorBiome = KFDustFXUtils.GetDustColor(vessel.mainBody, col, vessel.latitude, vessel.longitude);
			//colorBiome = _ModuleCameraShot._averageColour; //debug only. Forces camera colour only.
			if (KFPersistenceManager.isDustCameraEnabled)
			{
				colorCam = _ModuleCameraShot._averageColour;
				colorAverage = colorCam + colorBiome / 2;
			}
			else
				colorAverage = colorBiome;
			
			if (Equals(colorBiome, null))
				KFLog.Error("Color \"BiomeColor\" is null!", strClassName);
			if (speed >= minScrapeSpeed)
			{
				if (!Equals(colorAverage, colorDust))
				{
					Color[] colors = dustAnimator.colorAnimation;
					colors[0] = colorAverage;
					colors[1] = colorAverage;
					colors[2] = colorAverage;
					colors[3] = colorAverage;
					colors[4] = colorAverage;
					dustAnimator.colorAnimation = colors;
					colorDust = colorAverage;
				}
				kfdustFx.transform.position = contactPoint;
				kfdustFx.particleEmitter.maxEnergy = Mathf.Clamp(((speed / maxDustEnergyDiv) * tweakScaleCorrector), minDustEnergy, maxDustEnergy);
				// Energy is the thickness of the particles.
				kfdustFx.particleEmitter.maxEmission = Mathf.Clamp((speed * (maxDustEmissionMult * tweakScaleCorrector)), minDustEmission, (maxDustEmission * tweakScaleCorrector));
				// Emission is the number of particles emitted per second.
				kfdustFx.particleEmitter.maxSize = Mathf.Clamp((speed * tweakScaleCorrector), minDustSize, maxDustSize);
				// Size is self explanatory.  For wheels, I suggest values between 0.1 and 2.
				kfdustFx.particleEmitter.Emit();
			}
			return;
		}
		
		/// <summary>Called when the game enters a "paused" state.</summary>
		void OnPause()
		{
			isPaused = true;
			kfdustFx.particleEmitter.enabled = false;
		}
		
		/// <summary>Called when the game leaves a "paused" state.</summary>
		void OnUnpause()
		{
			isPaused = false;
			kfdustFx.particleEmitter.enabled = true;
		}
		
		/// <summary>Called when the object being referenced is destroyed, or when the module instance is deactivated.</summary>
		void OnDestroy()
		{
			//Debug.LogWarning(string.Format("{0}Stopped Audio.", logprefix));
			GameEvents.onGamePause.Remove(OnPause);
			//Debug.LogWarning(string.Format("{0}Removed OnPause hook.", logprefix));
			GameEvents.onGameUnpause.Remove(OnUnpause);
			//Debug.LogWarning(string.Format("{0}Removed OnUnPause hook.", logprefix));
		}
		
		/// <summary>Gets the current volume setting for Ship sounds.</summary>
		/// <returns>The volume value as a float.</returns>
		static float GetShipVolume()
		{
			return GameSettings.SHIP_VOLUME;
		}
	}
}
