﻿using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace KerbalFoundries
{
	public class KFWheel : PartModule
	{
		[KSPField(isPersistant = false, guiActive = false, guiName = "Suspension travel")]
		public float susTravel;

		// Config fields
		[KSPField]
		public string wheelName;
		[KSPField]
		public string colliderName;
		[KSPField]
		public string sustravName;
		[KSPField]
		public string steeringName;
		[KSPField]
		public bool useDirectionCorrector;
		[KSPField]
		public bool isSprocket;
		[KSPField]
		public bool hasSuspension = true;
		[KSPField]
		public float smoothSpeed = 40;
		[KSPField]
		public float rotationCorrection = 1;
		[KSPField]
		public bool trackedWheel = true;
		//default to tracked type (average of all colliders in contact with floor). This is OK for wheels, and will only need to be changed for multi wheeled parts that are not tracks.
		//wheel rotation axis
		[KSPField]
		public float wheelRotationX = 1;
		[KSPField]
		public float wheelRotationY;
		[KSPField]
		public float wheelRotationZ;
		//suspension traverse axis
		[KSPField]
		public string susTravAxis = "Y";
		[KSPField]
		public string steeringAxis = "Y";
		[KSPField(isPersistant = true)]
		public float lastFrameTraverse;

		//persistent fields. Not to be used for config
		[KSPField(isPersistant = true)]
		public float suspensionDistance;
		[KSPField(isPersistant = true)]
		public float suspensionSpring;
		[KSPField(isPersistant = true)]
		public float suspensionDamper;
		[KSPField(isPersistant = true)]
		public bool isConfigured;
        
        float tweakScaleCorrector = 1;

		//object types
		WheelCollider _wheelCollider;
		Transform _susTrav;
		Transform _wheel;
		Transform _trackSteering;
		KFModuleWheel _KFModuleWheel;

		//gloabl variables
		Vector3 initialPosition;
		Vector3 initialSteeringAngles;
		Vector3 _wheelRotation;
        
		int susTravIndex = 1;
		int steeringIndex = 1;
		public int directionCorrector = 1;
        
		float degreesPerTick;
		bool couroutinesActive;

		/// <summary>Logging utility.</summary>
		/// <remarks>Call using "KFLog.log_type"</remarks>
		readonly KFLogUtil KFLog = new KFLogUtil("KFWheel");
		
		//OnStart
		public override void OnStart(PartModule.StartState state)
		{
			_KFModuleWheel = part.GetComponentInChildren<KFModuleWheel>();
			tweakScaleCorrector = _KFModuleWheel.tweakScaleCorrector;
            
			if (!isConfigured)
			{
				foreach (WheelCollider wc in part.GetComponentsInChildren<WheelCollider>())
				{
					if (wc.name.StartsWith(colliderName, StringComparison.Ordinal))
					{
						_wheelCollider = wc;
						suspensionDistance = wc.suspensionDistance;
						//KFLog.Error(string.Format("SuspensionDistance is: {0}.", suspensionDistance));
						isConfigured = true;
					}
				}
			}
			// disable once RedundantIfElseBlock
			else
			{
				//KFLog.Error("Already configured - skipping.");
			}
            
			if (HighLogic.LoadedSceneIsEditor)
			{
				// Do absolutely nothing!
			}

			if (HighLogic.LoadedSceneIsFlight && !Equals(vessel.vesselType, VesselType.Debris) && vessel.parts.Count > 1)
			{
				GameEvents.onGamePause.Add(new EventVoid.OnEvent(OnPause));
				GameEvents.onGameUnpause.Add(new EventVoid.OnEvent(OnUnPause));
				//find named onjects in part
				foreach (WheelCollider wc in part.GetComponentsInChildren<WheelCollider>())
				{
					if (wc.name.StartsWith(colliderName, StringComparison.Ordinal))
						_wheelCollider = wc;
				}
				foreach (Transform tr in part.GetComponentsInChildren<Transform>())
				{
					if (tr.name.StartsWith(wheelName, StringComparison.Ordinal))
						_wheel = tr;
					if (tr.name.StartsWith(steeringName, StringComparison.Ordinal))
						_trackSteering = tr;
					if (tr.name.StartsWith(sustravName, StringComparison.Ordinal))
						_susTrav = tr;
				}
				//end find named objects

				initialPosition = _susTrav.localPosition;
				susTravIndex = Extensions.SetAxisIndex(susTravAxis);
				steeringIndex = Extensions.SetAxisIndex(steeringAxis); 

				if (_KFModuleWheel.hasSteering)
				{
					initialSteeringAngles = _trackSteering.transform.localEulerAngles;
					//KFLog.Log(initialSteeringAngles);
				}

				// Again, if/else can be made into a single line. - Gaalidas
				directionCorrector = useDirectionCorrector ? _KFModuleWheel.directionCorrector : 1;

				_wheelRotation = new Vector3(wheelRotationX, wheelRotationY, wheelRotationZ);

				if (Equals(lastFrameTraverse, 0)) //check to see if we have a value in persistance
				{
					//KFLog.Error("Last frame = 0. Setting");
					lastFrameTraverse = _wheelCollider.suspensionDistance;
					//KFLog.Error(lastFrameTraverse);
				}
				//KFLog.Error("Last frame =");
				//KFLog.Error(lastFrameTraverse);
				couroutinesActive = true;

				MoveSuspension(susTravIndex, -lastFrameTraverse, _susTrav); //to get the initial stuff correct

				if (_KFModuleWheel.hasSteering)
				{
					StartCoroutine("Steering");
					//KFLog.Error("starting steering coroutine");
				}
				if (trackedWheel)
					StartCoroutine("TrackedWheel");
				else
					StartCoroutine("IndividualWheel");

                if (hasSuspension)
                {
                    KFLog.Error("WARNING: KFWheel suspension module is deprecated. Please use KFSuspension");
                    StartCoroutine("Suspension");
                }
				
				part.force_activate();
			}//end flight
			base.OnStart(state);
		}
		//end OnStart
		
		//OnUpdate
		// disable FunctionNeverReturns
		IEnumerator Steering() //Coroutine for steering
		{
			while (true)
			{
				Vector3 newSteeringAngle = initialSteeringAngles;
				newSteeringAngle[steeringIndex] += _KFModuleWheel.steeringAngle;
				_trackSteering.transform.localEulerAngles = newSteeringAngle;
				yield return null;
			}
		}
		
		/// <summary>Coroutine for tracked wheels (all rotate the same speed in the part).</summary>
		IEnumerator TrackedWheel()
		{
			while (couroutinesActive)
			{
				_wheel.transform.Rotate(_wheelRotation, _KFModuleWheel.degreesPerTick * directionCorrector * rotationCorrection); //rotate wheel
				yield return null;
			}
		}
		
		/// <summary>Coroutine for individual wheels.</summary>
		IEnumerator IndividualWheel()
		{
			while (couroutinesActive)
			{
				degreesPerTick = (_wheelCollider.rpm / 60) * Time.deltaTime * 360;
				_wheel.transform.Rotate(_wheelRotation, degreesPerTick * directionCorrector * rotationCorrection); //rotate wheel
				yield return new WaitForFixedUpdate();
			}
		}

		/// <summary>Coroutine for wheels with suspension.</summary>
        /// <remarks>DEPRECATED!!!!!!!!!!!!!! Use KFSuspension instead!</remarks>
		IEnumerator Suspension()
		{
			while (true)
			{
				
				//suspension movement
				WheelHit hit; //set this up to grab sollider raycast info
				float frameTraverse = 0;
				bool grounded = _wheelCollider.GetGroundHit(out hit); //set up to pass out wheelhit coordinates
				//float tempLastFrameTraverse = lastFrameTraverse; //we need the value, but will over-write shortly. Store it here.
				if (grounded) //is it on the ground
				{
					frameTraverse = -_wheelCollider.transform.InverseTransformPoint(hit.point).y + _KFModuleWheel.raycastError - _wheelCollider.radius; //calculate suspension travel using the collider raycast.
                    
					if (frameTraverse > (_wheelCollider.suspensionDistance + _KFModuleWheel.raycastError)) //the raycast sometimes goes further than its max value. Catch and stop the mesh moving further
                        frameTraverse = _wheelCollider.suspensionDistance;
					else if (frameTraverse < -0.1) //the raycast can be negative (!); catch this too
                        frameTraverse = 0;
					
					lastFrameTraverse = frameTraverse;
				}
				else
					frameTraverse = lastFrameTraverse; //movement defaults back to last position when the collider is not grounded. Ungrounded collider returns suspension travel of zero!
				
				susTravel = frameTraverse; //debug only
				_susTrav.localPosition = initialPosition; //use the 
				MoveSuspension(susTravIndex, -frameTraverse, _susTrav);
				//end suspension movement
				yield return null; 
			}
		}

		public void OnPause()
		{
			couroutinesActive = false; // This will drop couroutines checking it out. StopCoroutine() will stop all instances, which is NOT good.
		}

		public void OnUnPause()
		{
			//KFLog.Warning("unpaused");
			couroutinesActive = true;
			try
			{
				if (trackedWheel)
					StartCoroutine("TrackedWheel");
				else
					StartCoroutine("IndividualWheel");
			}
			// disable once EmptyGeneralCatchClause
            catch
			{
			}
		}

		// Completely unnecessary override!
		/*public override void OnFixedUpdate()
		 *{
		 *	base.OnFixedUpdate();
		 *	//not a lot in here since I moved it all into coroutines.
		 *}
		 * //end OnFixedUpdate
		 */

		public void MoveSuspension(int index, float movement, Transform movedObject) //susTrav Axis, amount to move, named object.
		{
			var tempVector = new Vector3(0, 0, 0);
			tempVector[index] = movement * tweakScaleCorrector;
			movedObject.transform.Translate(tempVector, Space.Self);
		}
	}
	//end modele
}
//end class
