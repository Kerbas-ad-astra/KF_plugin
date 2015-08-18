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
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
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
		
		readonly KFLogUtil KFLog = new KFLogUtil("KFDustFX");
		
		/// <summary>The camera object we're using to get color info directly from the terrain.</summary>
		ModuleCameraShot _ModuleCameraShot;
		
		/// <summary>Scaling factor for effects</summary>
		public float scaleCorrector = 1;
		
		/// <summary>Specifies if the module is to be used for repulsors.</summary>
		/// <remarks>Default is "false"</remarks>
		//[KSPField]
		public bool isRepulsor = false;
		
		/// <summary>Mostly unnecessary, since there is no other purpose to having the module active.</summary>
		/// <remarks>Default is "true"</remarks>
		//[KSPField]
		public bool dustEffects = true;
		
		/// <summary>Minimum scrape speed.</summary>
		/// <remarks>Default is 0.5.  Repulsors should have this extremely low.</remarks>
		//[KSPField]
		public float minScrapeSpeed = 0.1f;
		
		/// <summary>Minimum dust energy value.</summary>
		/// <remarks>Default is 0.1.  Represents the minimum thickness of the particles.</remarks>
		//[KSPField]
		public float minDustEnergy = 0.1f;
		
		/// <summary>Minimum dust energy value.</summary>
		/// <remarks>Default is 1.  Represents the maximum thickness of the particles.</remarks>
		//[KSPField]
		public float maxDustEnergy = 1f;
		
		/// <summary>Minimum emission value of the dust particles.</summary>
		/// <remarks>Default is 0.1.  This is the number of particles to emit per second.</remarks>
		//[KSPField]
		public float minDustEmission = 0.1f;
		
		/// <summary>Maximum emission value of the dust particles.</summary>
		/// <remarks>Default is 20.  This is the number of particles to emit per second.</remarks>
		//[KSPField]
		public float maxDustEmission = 20f;
		
		/// <summary>Minimum size value of the dust particles.</summary>
		/// <remarks>Default is 0.1.  Represents the size of the particles themselves.</remarks>
		[KSPField]
		public float minDustSize = 0.1f;
		
		/// <summary>Maximum size value of the dust particles.</summary>
		/// <remarks>Default is 2.  Represents the size of the particles themselves.</remarks>
		//[KSPField]
		public float maxDustSize = 1.5f;
		
		/// <summary>Maximum emission energy divisor.</summary>
		/// <remarks>Default is 2.  Divides the thickness by the value provided.</remarks>
		//[KSPField]
		public float maxDustEnergyDiv = 2f;
		
		/// <summary>Maximum emission multiplier.</summary>
		/// <remarks>Default is 2.</remarks>
		//[KSPField]
		public float maxDustEmissionMult = 2f;
		
		/// <summary>Used in the OnCollisionEnter/Stay methods to define the minimum velocity magnitude to check against.</summary>
		/// <remarks>Default is 2.  Would set very low for repulsors.</remarks>
		//[KSPField]
		public float minVelocityMag = 2f;
		
		/// <summary>KSP path to the effect being used here.  Made into a field so that it can be customized in the future.</summary>
		/// <remarks>Default is "Effects/fx_smokeTrail_light"</remarks>
		[KSPField]
		public string dustEffectObject = "Effects/fx_smokeTrail_light";
		
		bool isPaused;
		GameObject kfdustFx;
		ParticleAnimator dustAnimator;
		Color colorDust;
		Color colorBiome;
		Color colorAverage;
		Color colorCam;
		Color colorWater;
        Color lastColorAverage;
		bool isColorOverrideActive;
		
		GameObject _kfRepLight;
		Light _repLight;

        Queue <Color> colorFIFO = new Queue<Color>();

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
			
			if (HighLogic.LoadedSceneIsFlight)
			{
				_ModuleCameraShot = vessel.rootPart.gameObject.GetComponent<ModuleCameraShot>();
                if (Equals(_ModuleCameraShot, null)) //add if not... sets some defaults.
                {
                    _ModuleCameraShot = vessel.rootPart.gameObject.AddComponent<ModuleCameraShot>();
                    _ModuleCameraShot.StartUp();
                }
				if (dustEffects)
					SetupParticles(isRepulsor);
			}
			
			GameEvents.onGamePause.Add(OnPause);
			GameEvents.onGameUnpause.Add(OnUnpause);
		}
		
		/// <summary>Defines the particle effects used in this module.</summary>
		public void SetupParticles(bool repulsor)
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
			if (KFPersistenceManager.isRepLightEnabled && repulsor)
				SetupLights();
		}
		
		public void SetupLights()
		{
			if (!KFPersistenceManager.isRepLightEnabled)
				return;
			_kfRepLight = new GameObject("Rep Light");
            _kfRepLight.transform.parent = this.part.gameObject.transform;
			_kfRepLight.transform.position = Vector3.zero;
			
			_repLight = _kfRepLight.AddComponent<Light>();
			_repLight.type = LightType.Point;
			_repLight.renderMode = LightRenderMode.ForceVertex;
			_repLight.range = 4.0f;
			_repLight.color = Color.blue;
			_repLight.intensity = 0.0f;
		}
		
		/// <summary>Called when the part is scraping over a surface.</summary>
		/// <param name="col">The collider being referenced.</param>
		/// <param name="position">The position of the scape.</param>
		public void WheelEmit(Vector3 position, Collider col)
		{
			if ((isPaused || Equals(part, null)) || Equals(part.rigidbody, null))
				return;
			float fMagnitude = part.rigidbody.velocity.magnitude;
			DustParticles(fMagnitude, position + (part.rigidbody.velocity * Time.deltaTime), col);
		}
		
		/// <summary>Contains information about what to do when the part stays in the collided state over a period of time.</summary>
		/// <param name="hitPoint">Point at which the collision takes place.</param>
		/// <param name="col">The collider being referenced.</param>
		/// <param name="force">Force of the hit.</param>
		/// <param name="normal">I got nothing here.</param>
		/// <param name="direction">Emission direction..</param>
		public void RepulsorEmit(Vector3 hitPoint, Collider col, float force, Vector3 normal, Vector3 direction)
		{
			isColorOverrideActive |= string.Equals("ModuleWaterSlider.Collider", col.gameObject.name);
			if (isPaused)
				return;

			//Scrape(hitPoint, col, force, normal, direction);
			DustParticles(force, hitPoint + (part.rigidbody.velocity * Time.deltaTime), col, normal, direction);
		}
		
		public void RepulsorLight(bool enabled, float squish)
		{
			if (KFPersistenceManager.isRepLightEnabled && enabled)
			{
				_kfRepLight.transform.localPosition = Vector3.zero;
				_repLight.intensity = Mathf.Clamp(squish, 0, 0.5f);
				_repLight.enabled = true;
			}
			else
			{
				_repLight.intensity = 0.0f;
				_repLight.enabled = false;
			}
		}

		/// <summary>This creates and maintains the dust particles and their body/biome specific colors.</summary>
		/// <param name="speed">Speed of the part which is scraping.</param>
		/// <param name="contactPoint">The point at which the collider and the scraped surface make contact.</param>
		/// <param name="col">The collider being referenced.</param>
		public void DustParticles(float speed, Vector3 contactPoint, Collider col) //(float force, Vector3 contactPoint, Collider col, Vector3 normal, Vector3 direction)
		{
			bool cameraEnabled = KFPersistenceManager.isDustCameraEnabled;
			colorWater = new Color(0.65f, 0.65f, 0.65f, 0.025f);
			
			if (!dustEffects || speed < minScrapeSpeed || Equals(dustAnimator, null) || !KFPersistenceManager.isDustEnabled)
				return;
			
			if (Equals(scaleCorrector, 0) || scaleCorrector < 0)
				scaleCorrector = 1f;
			colorBiome = KFDustFXUtils.GetDustColor(vessel.mainBody, col, vessel.latitude, vessel.longitude);
			
			cameraEnabled |= Equals(colorBiome, null);
            Color colorTemp = Color.black;
            //This needs to be averaged over a few frames. That gives us a few good things: the cahnge won't be so sharp on biome changes, and sampling the camera less often will look fine.
			if (cameraEnabled)
			{
				colorCam = _ModuleCameraShot._averageColour;
				colorTemp = colorCam + colorBiome; // Make the camera colour dominant.
                colorTemp.a = colorBiome.a;
			}
			else
				colorTemp = colorBiome;

            colorFIFO.Enqueue(colorTemp);

            if (colorFIFO.Count > 30)
            {
                colorFIFO.Dequeue();
            }

            Color[] colorArray = colorFIFO.ToArray();

            float r = 0;
            float g = 0;
            float b = 0;
            float a = 0;

            for (int i = 0; i < colorArray.Count(); i++)
            {
                r += colorArray[i].r;
                g += colorArray[i].g;
                b += colorArray[i].b;
                a += colorArray[i].a;
            }

            colorAverage = new Color(r/30, g/30, b/30, a/30);

            //colorAverage = (lastColorAverage + colorAverage) / 2;

            lastColorAverage = colorAverage;
			
			if (isColorOverrideActive)
				colorAverage = colorWater;
			
			if (speed >= minScrapeSpeed)
			{
				if (!Equals(colorAverage, colorDust))
				{
					Color[] colors = dustAnimator.colorAnimation;
                    colors[0] = new Color(colorAverage.r, colorAverage.g, colorAverage.b, colorAverage.a / 4);
                    colors[1] = new Color(colorAverage.r, colorAverage.g, colorAverage.b, colorAverage.a);
                    colors[2] = new Color(colorAverage.r, colorAverage.g, colorAverage.b, colorAverage.a);
                    colors[3] = new Color(colorAverage.r, colorAverage.g, colorAverage.b, colorAverage.a);
                    colors[4] = new Color(colorAverage.r, colorAverage.g, colorAverage.b, 0);
					dustAnimator.colorAnimation = colors;
					colorDust = colorAverage;
				}
				kfdustFx.transform.position = contactPoint;
                kfdustFx.particleEmitter.maxEnergy = Mathf.Clamp(speed / maxDustEnergyDiv, minDustEnergy, maxDustEnergy * scaleCorrector);
				// Energy is the thickness of the particles.
				kfdustFx.particleEmitter.maxEmission = Mathf.Clamp(speed * maxDustEmissionMult, minDustEmission, maxDustEmission * scaleCorrector);
				// Emission is the number of particles emitted per second.
                kfdustFx.particleEmitter.maxSize = Mathf.Clamp(speed * scaleCorrector, minDustSize, maxDustSize * scaleCorrector);
				// Size is self explanatory.  For wheels, I suggest values between 0.1 and 2.
				kfdustFx.particleEmitter.Emit();
			}
			return;
		}
		
		/// <summary>Repsulsor overload of Dustpartciles.</summary>
		/// <remarks>Adds direction and orientation.</remarks>
		/// <param name="force">Force of the repulsion field's collision with the surface.</param>
		/// <param name="contactPoint">Point of contact.</param>
		/// <param name="col">Collider making contact.</param>
		/// <param name="normal">No idea what this is.</param>
		/// <param name="direction">A direction to emit this stuff.</param>
		void DustParticles(float force, Vector3 contactPoint, Collider col, Vector3 normal, Vector3 direction)
		{
			if (!dustEffects || force < minScrapeSpeed || Equals(dustAnimator, null))
				return;
            
			kfdustFx.transform.rotation = Quaternion.Euler(normal);
			kfdustFx.particleEmitter.localVelocity = direction;
			DustParticles(force, contactPoint, col);
			
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
	}
}
