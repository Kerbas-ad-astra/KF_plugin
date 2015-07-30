﻿/*
 * KSP [0.23.5] Anti-Grav Repulsor plugin by Lo-Fi
 * Much inspiration and a couple of code snippets for deployment taken from BahamutoD's Critter Crawler mod. Huge respect, it's a fantastic mod :)
 * 
 */

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace KerbalFoundries
{
    public class KFRepulsor : PartModule
    {
        public JointSpring userspring;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Repulsor Settings")]
        public string settings = string.Empty;
        [KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
        public string status = "Nominal";
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Group Number"), UI_FloatRange(minValue = 0, maxValue = 10f, stepIncrement = 1f)]
        public float groupNumber = 1;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Height"), UI_FloatRange(minValue = 0, maxValue = 100f, stepIncrement = 5f)]
        public float rideHeight = 25;
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Strength"), UI_FloatRange(minValue = 0, maxValue = 6.00f, stepIncrement = 0.2f)]
        public float SpringRate;
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Damping"), UI_FloatRange(minValue = 0, maxValue = 0.3f, stepIncrement = 0.05f)]
        public float DamperRate;
        [KSPField]
        public bool deployed = true;
        [KSPField]
        public bool lowEnergy;
        [KSPField]
        public bool retractEffect;
        [KSPField]
        public bool pointDown;
        [KSPField]
        public string gridName;
        [KSPField]
        public string gimbalName;
        bool isReady;
        Transform _grid;
        Transform _gimbal;

        Vector3 _gridScale;
        
        //float effectPower; 
        const float effectPowerMax = 50f;
        float appliedRideHeight;
        float currentRideHeight;
        float repulsorCount = 0;

        KFRepulsorDustFX _dustFX;
        float dir;
        
        public List<WheelCollider> wcList = new List<WheelCollider>();
        //public List<float> susDistList = new List<float>();
        ModuleWaterSlider _MWS;

		/// <summary>Defines the rate at which the specified resource is consumed.</summary>
        [KSPField]
        public float resourceConsumptionRate = 1f;

		/// <summary>The name of the resource to consume.</summary>
        [KSPField]
		public string resourceName = "ElectricCharge";
        
        /// <summary>This is the info string that will display when the part info is shown.</summary>
		/// <remarks>This can be overridden in the part config for this module due to its KSPField status.</remarks>
		[KSPField]
        public string strPartInfo = "This part allows the craft to hover above the ground.  Steering mechanism not included.\n\n<b><color=#99ff00ff>Requires:</color></b>\n- {ResourceName}: {ConsumptionRate}/sec @ max height";
        public override string GetInfo()
		{
            return UpdateInfoText(strPartInfo, resourceName, resourceConsumptionRate);            
		}

        static string UpdateInfoText(string strPartInfo, string strResourceName, float consumptionRate)
        {
            return strPartInfo.Replace("{ResourceName}", strResourceName).Replace("{ConsumptionRate}", consumptionRate.ToString("0.00"));
        }
        
        //begin start
        public override void OnStart(PartModule.StartState state)  //when started
        {
            base.OnStart(state);
           
            _dustFX = this.part.GetComponent<KFRepulsorDustFX>(); //see if it's been added by MM
            if (Equals(_dustFX, null)) //add if not... sets some defaults.
            {
                part.AddModule("KFRepulsorDustFX");
                _dustFX = this.part.GetComponent<KFRepulsorDustFX>();
                //_dustFX.wheelImpact = true;
                //_dustFX.wheelImpactSound = "KerbalFoundries/Sounds/TyreSqueal";
                _dustFX.maxDustEmission = 28;
                _dustFX.OnStart(state);
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                _grid = transform.Search(gridName);
                _gridScale = _grid.transform.localScale;
                _gimbal = transform.Search(gimbalName);
                SetupWaterSlider();

                foreach (WheelCollider b in this.part.GetComponentsInChildren<WheelCollider>())
                {
                    repulsorCount ++;
                    userspring = b.suspensionSpring;
                    userspring.spring = SpringRate;
                    userspring.damper = DamperRate;
                    b.suspensionSpring = userspring;
                    b.suspensionDistance = 2.5f; //default to low setting to save stupid shenanigans on takeoff
                    wcList.Add(b);
                }
				KFLogUtil.Log(string.Format("Repulsor Count: {0}", repulsorCount), this);
				
                if (pointDown && Equals(vessel, FlightGlobals.ActiveVessel))
                {
                    StopAllCoroutines();
                    StartCoroutine("LookAt");
                }
                appliedRideHeight = rideHeight;
                StartCoroutine("UpdateHeight"); //start updating to height set before launch
                isReady = true;
            }
            DestroyBounds();
		}
		// End start

		void SetupWaterSlider()
		{
			_MWS = vessel.GetComponent<ModuleWaterSlider>();
		}

        /// <summary>A "Shrink" coroutine for steering.</summary>
        IEnumerator Shrink()
        {
            while (_grid.transform.localScale.x > 0.2f && _grid.transform.localScale.y > 0.2f && _grid.transform.localScale.z > 0.2f)
            {
                _grid.transform.localScale -= (_gridScale / 50);
                yield return null;
            }
            _grid.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            //Debug.LogWarning("Finished shrinking");
        }

        /// <summary>A "grow" coroutine for steering.</summary>
        IEnumerator Grow()
        {
            while (_grid.transform.localScale.x < _gridScale.x && _grid.transform.localScale.y < _gridScale.y && _grid.transform.localScale.z < _gridScale.z)
            {
                _grid.transform.localScale += (_gridScale / 50);
                yield return null;
            }
            //print(_gridScale);
            _grid.transform.localScale = _gridScale;
            //Debug.LogWarning("Finished growing");
        }

        // disable once FunctionNeverReturns
        /// <summary>A "LookAt" coroutine for steering.</summary>
        IEnumerator LookAt()
        {
            while (true)
            {
                _gimbal.transform.LookAt(FlightGlobals.ActiveVessel.mainBody.transform.position);
                yield return null;
            }
        }

        public void DestroyBounds()
        {
            Transform bounds = transform.Search("Bounds");
			if (!Equals(bounds, null))
            {
				UnityEngine.Object.Destroy(bounds.gameObject);
				print("Destroying Bounds.");
            }
        }

        public void RepulsorSound(float power)
        {
            part.Effect("RepulsorEffect", power / effectPowerMax);
        }

        public void UpdateWaterSlider()
        {
            _MWS.colliderHeight = -2.5f;
        }

        public void ResourceConsumption()
        {
            float resRequest = resourceConsumptionRate * Time.deltaTime * appliedRideHeight / 100f;
            float resDrain = part.RequestResource(resourceName, resRequest);
			lowEnergy = resDrain < resRequest ? true : false;
        }

        public void FixedUpdate()
        {
            if (!isReady)
                return;
            if (dir > 360)
                dir = 0;
            float sin = (float)Math.Sin(Mathf.Deg2Rad * dir);
            float cos = (float)Math.Cos(Mathf.Deg2Rad * dir);
            var emitDirection = new Vector3(0, sin * 10, cos * 10);
           
            float hitForce = 0;

            if (deployed && this.vessel.IsControllable)
            {
                // Reset the height of the water collider that slips away every frame.
                UpdateWaterSlider();
                ResourceConsumption();


                for (int i = 0; i < wcList.Count(); i++)
                {
                    WheelHit hit;
                    bool grounded = wcList[i].GetGroundHit(out hit);
                    if (grounded)
                    {
                        hitForce += hit.force;
                        _dustFX.RepulsorEmit(hit.point, hit.collider, hit.force, hit.normal, emitDirection);
                    }
                }

                if (lowEnergy)
                {
                    print("Retracting due to low Electric Charge");
                    appliedRideHeight = 0;
                    rideHeight = 0;
                    StartCoroutine("UpdateHeight");
                    status = "Low Charge";
                    deployed = false;
                }
                else
                {
                    status = "Nominal";
                }
            }
            else
            {
                //effectPower = 0;
				status = lowEnergy ? "Low Charge" : "Off";
            }
			
            RepulsorSound(hitForce);
            //effectPower = 0;    //reset to make sure it doesn't play when it shouldn't.
            //print(effectPower);

            dir += UnityEngine.Random.Range(20,60);
        }

        IEnumerator UpdateHeight()
        {
            if (appliedRideHeight > 0)
            {
				for (int i = 0; i < wcList.Count(); i++)
					wcList[i].enabled = true;
                deployed = true; // catches redeploy after low charge retract
                StopCoroutine("Shrink");
                StartCoroutine("Grow");
            }
			while (!Equals(Mathf.Round(currentRideHeight), appliedRideHeight))
			{
				currentRideHeight = Mathf.Lerp(currentRideHeight, appliedRideHeight, Time.deltaTime * 2);
				for (int i = 0; i < wcList.Count(); i++)
					wcList[i].suspensionDistance = appliedRideHeight / 20f;
				yield return new WaitForFixedUpdate();
			}
            if (currentRideHeight < 1)
            {
				for (int i = 0; i < wcList.Count(); i++)
					wcList[i].enabled = false;
                StopCoroutine("Grow");
                StartCoroutine("Shrink");
            }
            //Debug.LogWarning("Finished height update.");
        }

        [KSPAction("Retract")]
        public void Retract(KSPActionParam param)
        {
            if (rideHeight > 0f)
            {
                rideHeight -= 5f;
                //print("Retracting...");
                StartCoroutine("UpdateHeight");
            }
		}

        [KSPAction("Extend")]
        public void Extend(KSPActionParam param)
        {
            if (rideHeight < 100f)
            {
                rideHeight += 5f;
                //print("Extending...");
                StartCoroutine("UpdateHeight");
            }
		}

		[KSPAction("Apply Settings")]
		public void ApplySettingsAction(KSPActionParam param)
		{
			ApplySettings();
		}
		
        [KSPEvent(guiActive = true, guiName = "Apply Settings", active = true)]
        public void ApplySettings()
        {
            //appliedRideHeight = rideHeight;
            foreach (KFRepulsor mt in vessel.FindPartModulesImplementing<KFRepulsor>())
            {
                if (!Equals(groupNumber, 0) && Equals(groupNumber, mt.groupNumber))
                {
                    mt.rideHeight = rideHeight;
                    mt.appliedRideHeight = rideHeight;
                    mt.StartCoroutine("UpdateHeight"); 
                }
            }
            //StartCoroutine("UpdateHeight"); 
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Predict (group)")]
        public void PredictSettings()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                List<Part> repulsorList = EditorLogic.fetch.ship.parts.FindAll(part => part.Modules.Contains("KFRepulsor"));
                repulsorList.ForEach(part => part.SendMessage("CalculateCoM"));                
            }
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = true, guiName = "Predict (part)")]
        public void CalculateCoM()
        {
            Vector3 CoMposition = Vector3.zero;
            float shipMass = 0f;
            List<Part> repulsorList = new List<Part>();

            if (HighLogic.LoadedSceneIsEditor)
            {   
                foreach (Part part in EditorLogic.fetch.ship.parts)
                {
                    if (part.Modules.Contains("LaunchClamp") || part.physicalSignificance == Part.PhysicalSignificance.NONE) // ignore launch clamps and physicless parts
                        continue;

                    if (part.Modules.Contains("KFRepulsor")) // we'll need them later
                        repulsorList.Add(part);

                    float partMass = part.mass + part.GetModuleMass(part.mass) + part.GetResourceMass(); // a part's mass = it's mass + mass of all modules + mass of all resources
                    shipMass += partMass;

                    Vector3 partCoM = part.partTransform.position + part.partTransform.rotation * part.CoMOffset;
                    CoMposition += partCoM * partMass;
                }

                KFLogUtil.Log("CoMposition: " + CoMposition, "CalculateCoM()");
                KFLogUtil.Log("shipMass: " + shipMass, "CalculateCoM()");

            }
        }

	}
	// End class
}
// End namespace
