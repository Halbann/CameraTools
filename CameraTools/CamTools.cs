using KSP.UI.Screens;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System;
using UnityEngine;

namespace CameraTools
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class CamTools : MonoBehaviour
	{
		#region Fields
		public static CamTools fetch;

		string Version = "unknown";
		GameObject cameraParent;
		Vessel vessel;
		List<ModuleEngines> engines = new List<ModuleEngines>();
		List<ModuleCommand> cockpits = new List<ModuleCommand>();
		public static HashSet<VesselType> ignoreVesselTypesForAudio = new HashSet<VesselType> { VesselType.Debris, VesselType.SpaceObject, VesselType.Unknown, VesselType.Flag }; // Ignore some vessel types to avoid using up all the SoundManager's channels.
		Vector3 origPosition;
		Quaternion origRotation;
		Vector3 origLocalPosition;
		Quaternion origLocalRotation;
		Transform origParent;
		float origNearClip;
		float origDistance;
		FlightCamera.Modes origMode;
		float origFov = 60;
		FlightCamera flightCamera;
		Part camTarget = null;
		Vector3 cameraUp = Vector3.up;
		bool cameraToolActive = false;
		bool cameraParentWasStolen = false;
		System.Random rng;
		[CTPersistantField] public bool autoEnableForBDA = false;
		bool autoEnableOverriden = false;
		bool autoEnableOverride = false;
		bool cockpitView = false;
		Type bdCompetitionType = null;
		object bdCompetitionInstance = null;
		FieldInfo bdCompetitionStartingField = null;
		FieldInfo bdCompetitionIsActiveField = null;
		Type bdVesselSpawnerType = null;
		object bdVesselSpawnerInstance = null;
		FieldInfo bdVesselsSpawningField = null;
		Type bdBDATournamentType = null;
		object bdBDATournamentInstance = null;
		FieldInfo bdTournamentWarpInProgressField = null;
		Vessel.Situations lastVesselSituation = Vessel.Situations.FLYING;
		[CTPersistantField] public bool DEBUG = false;
		[CTPersistantField] public bool DEBUG2 = false;

		string message;
		bool vesselSwitched = false;
		bool BDAIFieldsNeedUpdating = true;
		float PositionInterpolationTypeMax = Enum.GetNames(typeof(PositionInterpolationType)).Length - 1;
		float RotationInterpolationTypeMax = Enum.GetNames(typeof(RotationInterpolationType)).Length - 1;

		Vector3 upAxis;
		Vector3 forwardAxis;
		Vector3 rightAxis;

		#region Input
		[CTPersistantField] public string cameraKey = "home";
		[CTPersistantField] public string revertKey = "end";
		[CTPersistantField] public string toggleMenu = "[0]";
		[CTPersistantField] public bool enableKeypad = false;
		[CTPersistantField] public string fmUpKey = "[7]";
		[CTPersistantField] public string fmDownKey = "[1]";
		[CTPersistantField] public string fmForwardKey = "[8]";
		[CTPersistantField] public string fmBackKey = "[5]";
		[CTPersistantField] public string fmLeftKey = "[4]";
		[CTPersistantField] public string fmRightKey = "[6]";
		[CTPersistantField] public string fmZoomInKey = "[9]";
		[CTPersistantField] public string fmZoomOutKey = "[3]";
		[CTPersistantField] public string fmMovementModifier = "enter";
		bool waitingForTarget = false;
		bool waitingForPosition = false;
		bool mouseUp = false;
		bool editingKeybindings = false;
		#endregion

		#region GUI
		public static bool guiEnabled = false;
		public static bool hasAddedButton = false;
		[CTPersistantField] public static bool textInput = false;
		bool updateFOV = false;
		float windowWidth = 250;
		float windowHeight = 400;
		float draggableHeight = 40;
		float leftIndent = 12;
		float entryHeight = 20;
		float contentTop = 20;
		float contentWidth;
		float keyframeEditorWindowHeight = 160f;
		[CTPersistantField] public ToolModes toolMode = ToolModes.StationaryCamera;
		[CTPersistantField] public bool randomMode = false;
		[CTPersistantField] public float randomModeDogfightChance = 75f;
		[CTPersistantField] public float randomModeIVAChance = 5f;
		[CTPersistantField] public float randomModeStationaryChance = 20f;
		[CTPersistantField] public float randomModePathingChance = 0f;
		Rect windowRect = new Rect(0, 0, 0, 0);
		bool gameUIToggle = true;
		float incrButtonWidth = 26;
		[CTPersistantField] public bool manualOffset = false;
		[CTPersistantField] public float manualOffsetForward = 500;
		[CTPersistantField] public float manualOffsetRight = 50;
		[CTPersistantField] public float manualOffsetUp = 5;
		string guiOffsetForward = "500";
		string guiOffsetRight = "50";
		string guiOffsetUp = "5";
		[CTPersistantField] public bool targetCoM = false;
		List<Tuple<double, string>> debugMessages = new List<Tuple<double, string>>();
		void DebugLog(string m) => debugMessages.Add(new Tuple<double, string>(Time.time, m));
		Rect cShadowRect = new Rect(Screen.width * 3 / 5, 100, Screen.width / 3 - 50, 100);
		Rect cDebugRect = new Rect(Screen.width * 3 / 5 + 2, 100 + 2, Screen.width / 3 - 50, 100);
		GUIStyle cStyle;
		GUIStyle cShadowStyle;
		GUIStyle centerLabel;
		GUIStyle leftLabel;
		GUIStyle rightLabel;
		GUIStyle leftLabelBold;
		GUIStyle titleStyle;
		GUIStyle inputFieldStyle;
		GUIStyle watermarkStyle;
		Dictionary<string, FloatInputField> inputFields;
		List<Tuple<double, string>> debug2Messages = new List<Tuple<double, string>>();
		void Debug2Log(string m) => debug2Messages.Add(new Tuple<double, string>(Time.time, m));
		float lastSavedTime = 0;

		#endregion

		#region Revert/Reset
		bool setPresetOffset = false;
		Vector3 presetOffset = Vector3.zero;
		[CTPersistantField] bool saveRotation = false;
		bool hasSavedRotation = false;
		Quaternion savedRotation;
		bool wasActiveBeforeModeChange = false;
		Vector3 lastTargetPosition = Vector3.zero;
		bool hasTarget = false;
		bool hasDied = false;
		float diedTime = 0;
		//retaining position and rotation after vessel destruction
		GameObject deathCam;
		Vector3 deathCamVelocity;
		Vector3d floatingKrakenAdjustment = Vector3d.zero; // Position adjustment for Floating origin and Krakensbane velocity changes.
		public delegate void ResetCTools();
		public static event ResetCTools OnResetCTools;
		#endregion

		#region Recording
		//recording input for key binding
		bool isRecordingInput = false;
		bool boundThisFrame = false;
		string currentlyBinding = "";
		#endregion

		#region Audio Fields
		AudioSource[] audioSources;
		float[] originalAudioSourceDoppler;
		HashSet<string> excludeAudioSources = new HashSet<string> { "MusicLogic", "windAS", "windHowlAS", "windTearAS", "sonicBoomAS" }; // Don't adjust music or atmospheric audio.
		bool hasSetDoppler = false;
		[CTPersistantField] public bool useAudioEffects = true;
		public static double speedOfSound = 330;
		#endregion

		#region Camera Shake
		Vector3 shakeOffset = Vector3.zero;
		float shakeMagnitude = 0;
		[CTPersistantField] public float shakeMultiplier = 1;
		#endregion

		#region Dogfight Camera Fields
		Vessel dogfightPrevTarget;
		Vessel dogfightTarget;
		[CTPersistantField] public float dogfightDistance = 30f;
		[CTPersistantField] public float dogfightOffsetX = 10f;
		[CTPersistantField] public float dogfightOffsetY = 4f;
		float dogfightMaxOffset = 50;
		[CTPersistantField] public float dogfightLerp = 0.2f;
		[CTPersistantField] public float dogfightRoll = 0f;
		Quaternion dogfightCameraRoll = Quaternion.identity;
		Vector3 dogfightCameraRollUp = Vector3.up;
		[CTPersistantField] public float autoZoomMarginDogfight = 20;
		[CTPersistantField] public float autoZoomMarginStationary = 20;
		public float autoZoomMargin
		{
			get
			{
				switch (toolMode)
				{
					case ToolModes.DogfightCamera: return autoZoomMarginDogfight;
					case ToolModes.StationaryCamera: return autoZoomMarginStationary;
					default: return 20f;
				}
			}
			set
			{
				switch (toolMode)
				{
					case ToolModes.DogfightCamera:
						autoZoomMarginDogfight = value;
						break;
					case ToolModes.StationaryCamera:
						autoZoomMarginStationary = value;
						break;
				}
			}
		}
		List<Vessel> loadedVessels;
		bool showingVesselList = false;
		bool dogfightLastTarget = false;
		Vector3 dogfightLastTargetPosition;
		Vector3 dogfightLastTargetVelocity;
		bool dogfightVelocityChase = false;
		//bdarmory
		bool hasBDAI = false;
		bool hasPilotAI = false;
		bool hasBDWM = false;
		[CTPersistantField] public bool useBDAutoTarget = false;
		object aiComponent = null;
		object wmComponent = null;
		FieldInfo bdAiTargetField;
		FieldInfo bdWmThreatField;
		FieldInfo bdWmMissileField;
		FieldInfo bdWmUnderFireField;
		FieldInfo bdWmUnderAttackField;
		double targetUpdateTime = 0;
		#endregion

		#region Stationary Camera Fields
		[CTPersistantField] public bool autoFlybyPosition = false;
		[CTPersistantField] public bool autoFOV = false;
		float manualFOV = 60;
		float currentFOV = 60;
		Vector3 manualPosition = Vector3.zero;
		Vector3 lastVesselCoM = Vector3.zero;
		[CTPersistantField] public float freeMoveSpeed = 10;
		string guiFreeMoveSpeed = "10";
		float freeMoveSpeedRaw;
		float freeMoveSpeedMinRaw;
		float freeMoveSpeedMaxRaw;
		[CTPersistantField] public float freeMoveSpeedMin = 0.1f;
		[CTPersistantField] public float freeMoveSpeedMax = 100f;
		[CTPersistantField] public float keyZoomSpeed = 1;
		string guiKeyZoomSpeed = "1";
		float zoomSpeedRaw;
		float zoomSpeedMinRaw;
		float zoomSpeedMaxRaw;
		public float zoomFactor = 1;
		[CTPersistantField] public float keyZoomSpeedMin = 0.01f;
		[CTPersistantField] public float keyZoomSpeedMax = 10f;
		[CTPersistantField] public float zoomExpDogfight = 1f;
		[CTPersistantField] public float zoomExpStationary = 1f;
		public float zoomExp
		{
			get
			{
				switch (toolMode)
				{
					case ToolModes.DogfightCamera: return zoomExpDogfight;
					case ToolModes.StationaryCamera: return zoomExpStationary;
					case ToolModes.Pathing: return zoomExpPathing;
					default: return 1f;
				}
			}
			set
			{
				switch (toolMode)
				{
					case ToolModes.DogfightCamera:
						zoomExpDogfight = value;
						break;
					case ToolModes.StationaryCamera:
						zoomExpStationary = value;
						break;
					case ToolModes.Pathing:
						zoomExpPathing = value;
						break;
				}
			}
		}
		[CTPersistantField] public float zoomExpPathing = 1f;
		[CTPersistantField] public float maxRelV = 2500;
		[CTPersistantField] public bool maintainInitialVelocity = false;
		Vector3d initialVelocity = Vector3d.zero;
		Orbit initialOrbit;
		[CTPersistantField] public bool useOrbital = false;
		float signedMaxRelVSqr;
		#endregion

		#region Pathing Camera Fields
		[CTPersistantField] public int selectedPathIndex = -1;
		List<CameraPath> availablePaths;
		CameraPath currentPath
		{
			get
			{
				if (selectedPathIndex >= 0 && selectedPathIndex < availablePaths.Count)
				{
					return availablePaths[selectedPathIndex];
				}
				else
				{
					return null;
				}
			}
		}
		int currentKeyframeIndex = -1;
		float currentKeyframeTime;
		PositionInterpolationType currentKeyframePositionInterpolationType = PositionInterpolationType.CubicSpline; // Default to CubicSpline
		RotationInterpolationType currentKeyframeRotationInterpolationType = RotationInterpolationType.CubicSpline; // Default to CubicSpline
		string currKeyTimeString;
		bool showKeyframeEditor = false;
		float pathStartTime;
		public float pathingSecondarySmoothing = 0f;
		public float pathingLerpRate = 1; // Lerp rate corresponding to the secondary smoothing factor.
		public float pathingTimeScale = 1f;
		bool isPlayingPath = false;
		float pathTime
		{
			get
			{
				return GetTime() - pathStartTime;
			}
		}
		Vector2 keysScrollPos;
		public bool interpolationType = false;
		[CTPersistantField] public bool useRealTime = true;
		#endregion
		#endregion

		void Awake()
		{
			if (fetch)
			{
				Destroy(fetch);
			}

			fetch = this;

			GetVersion();
			Load();

			rng = new System.Random();
		}

		void Start()
		{
			windowRect = new Rect(Screen.width - windowWidth - 40, 0, windowWidth, windowHeight);
			flightCamera = FlightCamera.fetch;
			if (flightCamera == null)
			{
				Debug.LogError("[CameraTools.CamTools]: Flight Camera is null! Unable to start CameraTools!");
				Destroy(this);
				return;
			}
			cameraToolActive = false;
			SaveOriginalCamera();

			AddToolbarButton();

			GameEvents.onHideUI.Add(GameUIDisable);
			GameEvents.onShowUI.Add(GameUIEnable);
			//GameEvents.onGamePause.Add (PostDeathRevert);
			GameEvents.OnVesselRecoveryRequested.Add(PostDeathRevert);
			GameEvents.onGameSceneLoadRequested.Add(PostDeathRevert);

			cameraParent = new GameObject("CameraToolsCameraParent");
			deathCam = new GameObject("CameraToolsDeathCam");

			CheckForBDA();
			DisableTimeControlsCameraZoomFix(); // Time Control's camera zoom fix breaks CameraTools.
			if (FlightGlobals.ActiveVessel != null)
			{
				cameraParent.transform.position = FlightGlobals.ActiveVessel.transform.position;
				vessel = FlightGlobals.ActiveVessel;
				deathCam.transform.position = vessel.transform.position;
				deathCam.transform.rotation = vessel.transform.rotation;

				CheckForBDAI(FlightGlobals.ActiveVessel);
				CheckForBDWM(FlightGlobals.ActiveVessel);
			}
			bdAiTargetField = GetAITargetField();
			bdWmThreatField = GetThreatField();
			bdWmMissileField = GetMissileField();
			bdWmUnderFireField = GetUnderFireField();
			bdWmUnderAttackField = GetUnderAttackField();
			GameEvents.onVesselChange.Add(SwitchToVessel);
			GameEvents.onVesselWillDestroy.Add(CurrentVesselWillDestroy);
			GameEvents.onVesselPartCountChanged.Add(VesselPartCountChanged);
			GameEvents.OnCameraChange.Add(CameraModeChange);
			TimingManager.FixedUpdateAdd(TimingManager.TimingStage.BetterLateThanNever, KrakensbaneWarpCorrection); // Perform our Krakensbane corrections after KSP's floating origin/Krakensbane corrections have run.

			// Styles and rects.
			cStyle = new GUIStyle(HighLogic.Skin.label);
			cStyle.fontStyle = UnityEngine.FontStyle.Bold;
			cStyle.fontSize = 18;
			cStyle.alignment = TextAnchor.UpperLeft;
			cShadowStyle = new GUIStyle(cStyle);
			cShadowRect = new Rect(cDebugRect);
			cShadowRect.x += 2;
			cShadowRect.y += 2;
			cShadowStyle.normal.textColor = new Color(0, 0, 0, 0.75f);
			centerLabel = new GUIStyle();
			centerLabel.alignment = TextAnchor.UpperCenter;
			centerLabel.normal.textColor = Color.white;
			leftLabel = new GUIStyle();
			leftLabel.alignment = TextAnchor.UpperLeft;
			leftLabel.normal.textColor = Color.white;
			rightLabel = new GUIStyle(leftLabel);
			rightLabel.alignment = TextAnchor.UpperRight;
			leftLabelBold = new GUIStyle(leftLabel);
			leftLabelBold.fontStyle = FontStyle.Bold;
			titleStyle = new GUIStyle(centerLabel);
			titleStyle.fontSize = 24;
			titleStyle.alignment = TextAnchor.MiddleCenter;
			watermarkStyle = new GUIStyle(leftLabel);
			watermarkStyle.normal.textColor = XKCDColors.LightBlueGrey;
			watermarkStyle.fontSize = 12;
			contentWidth = (windowWidth) - (2 * leftIndent);

			inputFields = new Dictionary<string, FloatInputField> {
				{"autoZoomMargin", gameObject.AddComponent<FloatInputField>().Initialise(0, autoZoomMargin, 0f, 50f)},
				{"zoomFactor", gameObject.AddComponent<FloatInputField>().Initialise(0, zoomFactor, 1f, 1096.63f)},
				{"shakeMultiplier", gameObject.AddComponent<FloatInputField>().Initialise(0, shakeMultiplier, 0f, 10f)},
				{"dogfightDistance", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightDistance, 1f, 100f)},
				{"dogfightOffsetX", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightOffsetX, -dogfightMaxOffset, dogfightMaxOffset)},
				{"dogfightOffsetY", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightOffsetY, -dogfightMaxOffset, dogfightMaxOffset)},
				{"dogfightLerp", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightLerp, 0.01f, 0.5f)},
				{"dogfightRoll", gameObject.AddComponent<FloatInputField>().Initialise(0, dogfightRoll, 0f, 1f)},
				{"pathingSecondarySmoothing", gameObject.AddComponent<FloatInputField>().Initialise(0, pathingSecondarySmoothing, 0f, 1f)},
				{"pathingTimeScale", gameObject.AddComponent<FloatInputField>().Initialise(0, pathingTimeScale, 0.05f, 4f)},
				{"randomModeDogfightChance", gameObject.AddComponent<FloatInputField>().Initialise(0, randomModeDogfightChance, 0f, 100f)},
				{"randomModeIVAChance", gameObject.AddComponent<FloatInputField>().Initialise(0, randomModeIVAChance, 0f, 100f)},
				{"randomModeStationaryChance", gameObject.AddComponent<FloatInputField>().Initialise(0, randomModeStationaryChance, 0f, 100f)},
				{"randomModePathingChance", gameObject.AddComponent<FloatInputField>().Initialise(0, randomModePathingChance, 0f, 100f)},
				{"freeMoveSpeed", gameObject.AddComponent<FloatInputField>().Initialise(0, freeMoveSpeed, freeMoveSpeedMin, freeMoveSpeedMax)},
				{"keyZoomSpeed", gameObject.AddComponent<FloatInputField>().Initialise(0, keyZoomSpeed, keyZoomSpeedMin, keyZoomSpeedMax)},
				{"maxRelV", gameObject.AddComponent<FloatInputField>().Initialise(0, maxRelV)},
			};
		}

		void OnDestroy()
		{
			GameEvents.onVesselChange.Remove(SwitchToVessel);
			GameEvents.onVesselWillDestroy.Remove(CurrentVesselWillDestroy);
			GameEvents.onVesselPartCountChanged.Remove(VesselPartCountChanged);
			GameEvents.OnCameraChange.Remove(CameraModeChange);
			TimingManager.FixedUpdateRemove(TimingManager.TimingStage.BetterLateThanNever, KrakensbaneWarpCorrection);
			Save();
		}

		void CameraModeChange(CameraManager.CameraMode mode)
		{
			if (mode != CameraManager.CameraMode.Flight && CameraManager.Instance.previousCameraMode == CameraManager.CameraMode.Flight)
			{
				wasActiveBeforeModeChange = cameraToolActive;
				cameraToolActive = false;
			}
			else if (mode == CameraManager.CameraMode.Flight)
			{
				if (wasActiveBeforeModeChange && !autoEnableOverriden && !autoEnableOverride)
				{
					Debug.Log("[CameraTools]: Camera mode changed to " + mode + ", reactivating " + toolMode + ".");
					cockpitView = false; // Don't go back into cockpit view in case it was triggered by the user.
					cameraToolActive = true;
					RevertCamera();
					flightCamera.transform.position = deathCam.transform.position;
					flightCamera.transform.rotation = deathCam.transform.rotation;
					cameraActivate();
				}
			}
		}

		HashSet<Vessel.Situations> krakensbaneWarpSituations = new HashSet<Vessel.Situations> { Vessel.Situations.LANDED, Vessel.Situations.SPLASHED, Vessel.Situations.PRELAUNCH, Vessel.Situations.FLYING };
		void KrakensbaneWarpCorrection()
		{
			if (cameraToolActive)
			{
				// Compensate for floating origin and Krakensbane velocity shifts under warp.
				// This runs in the BetterLateThanNever timing phase after the flight integrator and floating origin/Krakensbane corrections have been applied.

				switch (toolMode)
				{
					case ToolModes.DogfightCamera:
						floatingKrakenAdjustment = -FloatingOrigin.Offset;
						if (vessel.situation!=lastVesselSituation) DebugLog($"Vessel Situation changed from {lastVesselSituation} to {vessel.situation}");
						if (vessel != null && krakensbaneWarpSituations.Contains(lastVesselSituation) || vessel.situation != lastVesselSituation) floatingKrakenAdjustment += (vessel.Velocity() - Krakensbane.GetFrameVelocity()) * TimeWarp.fixedDeltaTime;
						cameraParent.transform.position += floatingKrakenAdjustment;
						dogfightLastTargetPosition += floatingKrakenAdjustment;
						break;
				}
				if (DEBUG && TimeWarp.WarpMode == TimeWarp.Modes.LOW && FloatingOrigin.Offset.sqrMagnitude > 10)
				{
					message = "Floating origin offset: " + FloatingOrigin.Offset.ToString("0.0") + ", Krakensbane velocity correction: " + Krakensbane.GetLastCorrection().ToString("0.0");
					DebugLog(message);
					Debug.Log("[CameraTools]: DEBUG " + message);
				}
			}
		}

		void Update()
		{
			if (!isRecordingInput && !boundThisFrame)
			{
				if (Input.GetKeyDown(toggleMenu))
				{
					ToggleGui();
				}

				if (Input.GetKeyDown(revertKey))
				{
					autoEnableOverriden = true;
					RevertCamera();
				}
				else if (Input.GetKeyDown(cameraKey))
				{
					autoEnableOverriden = false;
					if (!cameraToolActive && randomMode)
					{
						ChooseRandomMode();
					}
					cameraActivate();
				}
			}

			if (Input.GetMouseButtonUp(0))
			{
				mouseUp = true;
			}

			//get target transform from mouseClick
			if (waitingForTarget && mouseUp && Input.GetKeyDown(KeyCode.Mouse0))
			{
				Part tgt = GetPartFromMouse();
				if (tgt != null)
				{
					camTarget = tgt;
					hasTarget = true;
				}
				else
				{
					Vector3 pos = GetPosFromMouse();
					if (pos != Vector3.zero)
					{
						lastTargetPosition = pos;
						hasTarget = true;
					}
				}

				waitingForTarget = false;
			}

			//set position from mouseClick
			if (waitingForPosition && mouseUp && Input.GetKeyDown(KeyCode.Mouse0))
			{
				Vector3 pos = GetPosFromMouse();
				if (pos != Vector3.zero)// && isStationaryCamera)
				{
					presetOffset = pos;
					setPresetOffset = true;
				}
				else Debug.Log("[CameraTools]: No pos from mouse click");

				waitingForPosition = false;
			}

			if (cameraToolActive)
			{
				switch (toolMode)
				{
					case ToolModes.StationaryCamera:
						UpdateStationaryCamera();
						break;
					case ToolModes.Pathing:
						if (useRealTime)
							UpdatePathingCam();
						break;
					default: // Other modes are handled in FixedUpdate due to relying on interpolation of positions updated in the physics update.
						break;
				}
			}
			boundThisFrame = false;
		}

		void FixedUpdate()
		{
			// Note: we have to perform several of the camera adjustments during FixedUpdate to avoid jitter in the Lerps in the camera position and rotation due to inconsistent numbers of physics updates per frame.
			if (!FlightGlobals.ready) return;
			if (CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.Flight) return;

			if (cameraToolActive)
			{
				if ((!hasDied && flightCamera.transform.parent != cameraParent.transform) || (hasDied && flightCamera.transform.parent != deathCam.transform))
				{
					message = "Someone has stolen the camera parent! Abort!";
					Debug.Log("[CameraTools]: " + message);
					if (DEBUG) DebugLog(message);
					cameraToolActive = false;
					cameraParentWasStolen = true;
					RevertCamera();
				}
			}

			if (hasDied && cameraToolActive) return; // Do nothing until we have an active vessel.

			if (vessel == null || vessel != FlightGlobals.ActiveVessel)
			{
				vessel = FlightGlobals.ActiveVessel;
			}

			if (autoEnableForBDA && !autoEnableOverriden && (toolMode != ToolModes.Pathing || (selectedPathIndex >= 0 && currentPath.keyframeCount > 0)))
			{
				AutoEnableForBDA();
			}
			if (cameraToolActive)
			{
				lastVesselSituation = vessel.situation;
				switch (toolMode)
				{
					case ToolModes.DogfightCamera:
						UpdateDogfightCamera();
						if (dogfightTarget && dogfightTarget.isActiveVessel)
						{
							dogfightTarget = null;
							if (cameraToolActive) // UpdateDogfightCamera may disable the camera.
							{
								if (DEBUG) Debug.Log("[CameraTools]: Reverting because dogfightTarget is null");
								RevertCamera();
							}
						}
						break;
					case ToolModes.StationaryCamera:
						if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
						{
							if (randomMode || FloatingOrigin.OffsetNonKrakensbane.sqrMagnitude < maxRelVSqr * TimeWarp.fixedDeltaTime * TimeWarp.fixedDeltaTime) // Account for maxRelV unless in random mode.
							{ lastVesselCoM -= FloatingOrigin.OffsetNonKrakensbane; }
							else // If the floating origin is not fixed, then it moves with the current vessel.
							{ lastVesselCoM -= maxRelV * TimeWarp.fixedDeltaTime * FloatingOrigin.OffsetNonKrakensbane.normalized; }
						}
						if (maintainInitialVelocity && !randomMode) // Don't maintain velocity when using random mode.
						{
							if (useOrbital && initialOrbit != null)
							{ lastVesselCoM += initialOrbit.getOrbitalVelocityAtUT(Planetarium.GetUniversalTime()).xzy * TimeWarp.fixedDeltaTime; }
							else
							{ lastVesselCoM += initialVelocity * TimeWarp.fixedDeltaTime; }
						}
						// Updating the stationary camera is handled in Update.
						break;
					case ToolModes.Pathing:
						if (!useRealTime) UpdatePathingCam();
						break;
					default:
						break;
				}
			}
			else
			{
				if (!autoFOV)
				{
					zoomFactor = Mathf.Exp(zoomExp) / Mathf.Exp(1);
				}
			}
		}

		void LateUpdate()
		{
			UpdateCameraShake(); // Update camera shake each frame so that it dies down.
			if (hasDied && cameraToolActive)
			{
				deathCam.transform.position += deathCamVelocity * TimeWarp.deltaTime;// + floatingKrakenAdjustment;
				deathCamVelocity *= 0.95f;
				if (flightCamera.transform.parent != deathCam.transform) // Something else keeps trying to steal the camera after the vessel has died, so we need to keep overriding it.
				{
					SetDeathCam();
				}
			}
			else if (!vesselSwitched)
			{
				switch (CameraManager.Instance.currentCameraMode)
				{
					case CameraManager.CameraMode.IVA:
						var IVACamera = CameraManager.GetCurrentCamera();
						deathCam.transform.position = IVACamera.transform.position;
						deathCam.transform.rotation = IVACamera.transform.rotation;
						break;
					case CameraManager.CameraMode.Flight:
						deathCam.transform.position = flightCamera.transform.position;
						deathCam.transform.rotation = flightCamera.transform.rotation;
						break;
				}
			}
			if (cameraToolActive && vesselSwitched) // We perform this here instead of waiting for the next frame to avoid a flicker of the camera being switched during a FixedUpdate.
			{
				vesselSwitched = false;
				cameraActivate();
			}
		}

		private void cameraActivate()
		{
			if (DEBUG) { Debug.Log("[CameraTools]: Activating camera."); DebugLog("Activating camera"); }
			if (!cameraToolActive && !cameraParentWasStolen)
			{
				SaveOriginalCamera();
			}
			deathCam.transform.position = flightCamera.transform.position;
			deathCam.transform.rotation = flightCamera.transform.rotation;
			if (toolMode == ToolModes.StationaryCamera)
			{
				StartStationaryCamera();
			}
			else if (toolMode == ToolModes.DogfightCamera)
			{
				StartDogfightCamera();
			}
			else if (toolMode == ToolModes.Pathing)
			{
				StartPathingCam();
				PlayPathingCam();
			}
		}

		#region Dogfight Camera
		void StartDogfightCamera()
		{
			toolMode = ToolModes.DogfightCamera;
			if (FlightGlobals.ActiveVessel == null)
			{
				Debug.Log("[CameraTools]: No active vessel.");
				return;
			}
			if (DEBUG) { Debug.Log("[CameraTools]: Starting dogfight camera."); DebugLog("Starting dogfight camera"); }

			if (!dogfightTarget)
			{
				if (false && randomMode && rng.Next(3) == 0)
				{
					dogfightVelocityChase = false; // sometimes throw in a non chase angle
				}
				else
				{
					dogfightVelocityChase = true;
				}
			}
			else
			{
				dogfightVelocityChase = false;
			}

			dogfightPrevTarget = dogfightTarget;

			hasDied = false;
			vessel = FlightGlobals.ActiveVessel;
			cameraUp = -FlightGlobals.getGeeForceAtPosition(vessel.CoM).normalized;

			if (flightCamera.transform.parent != cameraParent.transform)
			{
				SetCameraParent(deathCam.transform, true); // First update the cameraParent to the last deathCam configuration
			}

			cameraToolActive = true;

			ResetDoppler();
			if (OnResetCTools != null)
			{ OnResetCTools(); }
			SetDoppler(false);
			AddAtmoAudioControllers(false);
		}

		void UpdateDogfightCamera()
		{
			if (!vessel || (!dogfightTarget && !dogfightLastTarget && !dogfightVelocityChase))
			{
				if (DEBUG) { Debug.Log("[CameraTools]: Reverting during UpdateDogfightCamera"); }
				RevertCamera();
				return;
			}

			if (cockpitView)
			{
				if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) // Already enabled, do nothing.
				{ return; }
				// Check that there's still a kerbal to switch to.
				if (cockpits.Any(cockpit => cockpit != null && cockpit.part != null && cockpit.part.protoModuleCrew.Count > 0))
				{
					try
					{
						CameraManager.Instance.SetCameraIVA(); // Try to enable IVA camera.
					}
					catch (Exception e)
					{
						Debug.LogError($"[CameraTools.CamTools]: Exception thrown trying to set IVA camera mode, aborting. {e.Message}");
						cockpitView = false;
					}
				}
				else
					cockpitView = false;
				if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) // Success!
				{ return; }
			}

			if (dogfightTarget)
			{
				dogfightLastTarget = true;
				dogfightLastTargetPosition = dogfightTarget.CoM;
				dogfightLastTargetVelocity = dogfightTarget.Velocity();
			}
			else if (dogfightLastTarget)
			{
				dogfightLastTargetPosition += dogfightLastTargetVelocity * TimeWarp.fixedDeltaTime;
			}

			cameraParent.transform.position = vessel.CoM;

			if (dogfightVelocityChase)
			{
				var lastDogfightLastTargetPosition = dogfightLastTargetPosition;
				if (vessel.Speed() > 1)
				{
					dogfightLastTargetPosition = vessel.CoM + vessel.Velocity().normalized * 5000;
				}
				else
				{
					dogfightLastTargetPosition = vessel.CoM + vessel.ReferenceTransform.up * 5000;
				}
				if (vessel.Splashed && vessel.Speed() < 10) // Don't bob around lots if the vessel is in water.
				{
					dogfightLastTargetPosition = Vector3.Lerp(lastDogfightLastTargetPosition, Vector3.ProjectOnPlane(dogfightLastTargetPosition, cameraUp), (float)vessel.Speed() * 0.01f); // Slow lerp to a horizontal position.
				}
			}

			//roll
			if (dogfightRoll > 0)
			{
				var vesselRollTarget = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(cameraUp, -vessel.ReferenceTransform.forward), dogfightRoll * Vector3.Angle(cameraUp, -vessel.ReferenceTransform.forward));
				dogfightCameraRoll = Quaternion.Lerp(dogfightCameraRoll, vesselRollTarget, dogfightLerp);
				dogfightCameraRollUp = dogfightCameraRoll * cameraUp;
			}
			else
			{
				dogfightCameraRollUp = cameraUp;
			}

			Vector3 offsetDirection = Vector3.Cross(dogfightCameraRollUp, dogfightLastTargetPosition - vessel.CoM).normalized; // FIXME This is changing when in high warp mode. Also, check this when suborbital (not changing).
			Vector3 camPos = vessel.CoM + ((vessel.CoM - dogfightLastTargetPosition).normalized * dogfightDistance) + (dogfightOffsetX * offsetDirection) + (dogfightOffsetY * dogfightCameraRollUp);

			Vector3 localCamPos = cameraParent.transform.InverseTransformPoint(camPos);
			flightCamera.transform.localPosition = Vector3.Lerp(flightCamera.transform.localPosition, localCamPos, dogfightLerp);
			if (DEBUG2 && Time.deltaTime > 0)
			{
				var cmb = FlightGlobals.currentMainBody;
				debug2Messages.Clear();
				Debug2Log("situation: " + vessel.situation);
				Debug2Log("warp mode: " + TimeWarp.WarpMode + ", warp factor: " + TimeWarp.CurrentRate);
				Debug2Log($"radius: {cmb.Radius}, radiusAtmoFactor: {cmb.radiusAtmoFactor}, atmo: {cmb.atmosphere}, atmoDepth: {cmb.atmosphereDepth}");
				Debug2Log("speed: " + vessel.Speed().ToString("G3") + ", vel: " + vessel.Velocity().ToString("G3"));
				Debug2Log("offsetDirection: " + offsetDirection.ToString("G3"));
				Debug2Log("target offset: " + ((vessel.CoM - dogfightLastTargetPosition).normalized * dogfightDistance).ToString("G3"));
				Debug2Log("xOff: " + (dogfightOffsetX * offsetDirection).ToString("G3"));
				Debug2Log("yOff: " + (dogfightOffsetY * dogfightCameraRollUp).ToString("G3"));
				Debug2Log("camPos - vessel.CoM: " + (camPos - vessel.CoM).ToString("G3"));
				Debug2Log("localCamPos: " + localCamPos.ToString("G3") + ", " + flightCamera.transform.localPosition.ToString("G3"));
				Debug2Log("offset from vessel CoM: " + (flightCamera.transform.position - vessel.CoM).ToString("G3"));
				Debug2Log("camParentPos - flightCamPos: " + (cameraParent.transform.position - flightCamera.transform.position).ToString("G3"));
				Debug2Log("vessel velocity: " + vessel.Velocity().ToString("G3") + ", Kraken velocity: " + Krakensbane.GetFrameVelocity().ToString("G3"));
				Debug2Log("(vv - kv): " + (vessel.Velocity() - Krakensbane.GetFrameVelocity()).ToString("G3") + ", ΔKv: " + Krakensbane.GetLastCorrection().ToString("G3"));
				Debug2Log("(vv - kv)*Δt: " + ((vessel.Velocity() - Krakensbane.GetFrameVelocity()) * TimeWarp.fixedDeltaTime).ToString("G3"));
				Debug2Log("floating origin offset: " + FloatingOrigin.Offset.ToString("G3") + ", offsetNonKB: " + FloatingOrigin.OffsetNonKrakensbane.ToString("G3"));
				Debug2Log("floatingKrakenAdjustment: " + floatingKrakenAdjustment.ToString("G3"));
			}

			//rotation
			Quaternion vesselLook = Quaternion.LookRotation(vessel.CoM - flightCamera.transform.position, dogfightCameraRollUp);
			Quaternion targetLook = Quaternion.LookRotation(dogfightLastTargetPosition - flightCamera.transform.position, dogfightCameraRollUp);
			Quaternion camRot = Quaternion.Lerp(vesselLook, targetLook, 0.5f);
			flightCamera.transform.rotation = Quaternion.Lerp(flightCamera.transform.rotation, camRot, dogfightLerp);

			//autoFov
			if (autoFOV)
			{
				float targetFoV;
				if (dogfightVelocityChase)
				{
					targetFoV = Mathf.Clamp((7000 / (dogfightDistance + 100)) - 14 + autoZoomMargin, 2, 60);
				}
				else
				{
					float angle = Vector3.Angle(dogfightLastTargetPosition - flightCamera.transform.position, vessel.CoM - flightCamera.transform.position);
					targetFoV = Mathf.Clamp(angle + autoZoomMargin, 0.1f, 60f);
				}
				manualFOV = targetFoV;
			}
			//FOV
			if (!autoFOV)
			{
				zoomFactor = Mathf.Exp(zoomExp) / Mathf.Exp(1);
				manualFOV = 60 / zoomFactor;
				updateFOV = (currentFOV != manualFOV);
				if (updateFOV)
				{
					currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
					flightCamera.SetFoV(currentFOV);
					updateFOV = false;
				}
			}
			else
			{
				currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
				flightCamera.SetFoV(currentFOV);
				zoomFactor = 60 / currentFOV;
			}

			//free move
			if (enableKeypad && !boundThisFrame)
			{
				if (Input.GetKey(fmUpKey))
				{
					dogfightOffsetY += freeMoveSpeed * Time.fixedDeltaTime;
					dogfightOffsetY = Mathf.Clamp(dogfightOffsetY, -dogfightMaxOffset, dogfightMaxOffset);
				}
				else if (Input.GetKey(fmDownKey))
				{
					dogfightOffsetY -= freeMoveSpeed * Time.fixedDeltaTime;
					dogfightOffsetY = Mathf.Clamp(dogfightOffsetY, -dogfightMaxOffset, dogfightMaxOffset);
				}
				if (Input.GetKey(fmForwardKey))
				{
					dogfightDistance -= freeMoveSpeed * Time.fixedDeltaTime;
					dogfightDistance = Mathf.Clamp(dogfightDistance, 1f, 100f);
				}
				else if (Input.GetKey(fmBackKey))
				{
					dogfightDistance += freeMoveSpeed * Time.fixedDeltaTime;
					dogfightDistance = Mathf.Clamp(dogfightDistance, 1f, 100f);
				}
				if (Input.GetKey(fmLeftKey))
				{
					dogfightOffsetX -= freeMoveSpeed * Time.fixedDeltaTime;
					dogfightOffsetX = Mathf.Clamp(dogfightOffsetX, -dogfightMaxOffset, dogfightMaxOffset);
				}
				else if (Input.GetKey(fmRightKey))
				{
					dogfightOffsetX += freeMoveSpeed * Time.fixedDeltaTime;
					dogfightOffsetX = Mathf.Clamp(dogfightOffsetX, -dogfightMaxOffset, dogfightMaxOffset);
				}

				//keyZoom
				if (!autoFOV)
				{
					if (Input.GetKey(fmZoomInKey))
					{
						zoomExp = Mathf.Clamp(zoomExp + (keyZoomSpeed * Time.fixedDeltaTime), 1, 8);
					}
					else if (Input.GetKey(fmZoomOutKey))
					{
						zoomExp = Mathf.Clamp(zoomExp - (keyZoomSpeed * Time.fixedDeltaTime), 1, 8);
					}
				}
				else
				{
					if (Input.GetKey(fmZoomInKey))
					{
						autoZoomMargin = Mathf.Clamp(autoZoomMargin + (keyZoomSpeed * 10 * Time.fixedDeltaTime), 0, 50);
					}
					else if (Input.GetKey(fmZoomOutKey))
					{
						autoZoomMargin = Mathf.Clamp(autoZoomMargin - (keyZoomSpeed * 10 * Time.fixedDeltaTime), 0, 50);
					}
				}
			}

			//vessel camera shake
			if (shakeMultiplier > 0)
			{
				foreach (var v in FlightGlobals.Vessels)
				{
					if (!v || !v.loaded || v.packed || v.isActiveVessel) continue;
					VesselCameraShake(v);
				}
			}

			if (hasBDAI && useBDAutoTarget)
			{
				// Check for missile
				if (Planetarium.GetUniversalTime() - targetUpdateTime > 0.1f && BDAIFieldsNeedUpdating)
				{ bdWmMissileField = GetMissileField(); }

				// don't update targets too quickly, unless we're under attack by a missile
				if ((bdWmMissileField != null) || (Planetarium.GetUniversalTime() - targetUpdateTime > 3))
				{
					UpdateAIDogfightTarget();
				}
			}

			if (dogfightTarget != dogfightPrevTarget)
			{
				StartDogfightCamera();
			}
		}

		Vessel newAITarget = null;
		void UpdateAIDogfightTarget()
		{
			if (hasBDAI && hasBDWM && useBDAutoTarget)
			{
				newAITarget = GetAITargetedVessel();
				if (newAITarget)
				{
					if (DEBUG && dogfightTarget != newAITarget)
					{
						message = "Switching dogfight target to " + newAITarget.vesselName + (dogfightTarget != null ? " from " + dogfightTarget.vesselName : "");
						Debug.Log("[CameraTools]: " + message);
						DebugLog(message);
					}
					dogfightTarget = newAITarget;
				}
				targetUpdateTime = Planetarium.GetUniversalTime();
			}
		}
		#endregion

		#region Stationary Camera
		void StartStationaryCamera()
		{
			toolMode = ToolModes.StationaryCamera;
			if (FlightGlobals.ActiveVessel != null)
			{
				if (DEBUG)
				{
					message = "Starting stationary camera.";
					Debug.Log("[CameraTools]: " + message);
					DebugLog(message);
				}
				hasDied = false;
				vessel = FlightGlobals.ActiveVessel;
				cameraUp = -FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).normalized;
				if (FlightCamera.fetch.mode == FlightCamera.Modes.ORBITAL || (FlightCamera.fetch.mode == FlightCamera.Modes.AUTO && FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL))
				{
					cameraUp = Vector3.up;
				}
				rightAxis = -Vector3.Cross(vessel.srf_velocity, vessel.upAxis).normalized;

				if (flightCamera.transform.parent != cameraParent.transform)
				{
					SetCameraParent(deathCam.transform, true); // First update the cameraParent to the last deathCam configuration
				}

				manualPosition = Vector3.zero;
				if (randomMode)
				{
					camTarget = FlightGlobals.ActiveVessel.GetReferenceTransformPart();
					if (camTarget == null) // Sometimes the vessel doesn't have the reference transform part set up. It ought to be the root part usually.
						camTarget = FlightGlobals.ActiveVessel.rootPart;
				}
				hasTarget = (camTarget != null) ? true : false;
				lastVesselCoM = vessel.CoM;

				// Camera position.
				if (autoFlybyPosition || randomMode)
				{
					setPresetOffset = false;

					float clampedSpeed = Mathf.Clamp((float)vessel.srfSpeed, 0, Mathf.Abs(maxRelV));
					float sideDistance = Mathf.Clamp(20 + (clampedSpeed / 10), 20, 150);
					float distanceAhead = Mathf.Clamp(4 * clampedSpeed, 30, 3500) * Mathf.Sign(maxRelV);

					if (vessel.Velocity().sqrMagnitude > 1)
					{ flightCamera.transform.position = vessel.transform.position + distanceAhead * vessel.Velocity().normalized; }
					else
					{ flightCamera.transform.position = vessel.transform.position + distanceAhead * vessel.vesselTransform.up; }

					if (flightCamera.mode == FlightCamera.Modes.FREE || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.FREE)
					{
						flightCamera.transform.position += (sideDistance * rightAxis) + (15 * cameraUp);
					}
					else if (flightCamera.mode == FlightCamera.Modes.ORBITAL || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL)
					{
						flightCamera.transform.position += (sideDistance * FlightGlobals.getUpAxis()) + (15 * Vector3.up);
					}

					var cameraRadarAltitude = GetRadarAltitudeAtPos(flightCamera.transform.position);
					if (vessel.radarAltitude > 0f && vessel.radarAltitude < -3d * vessel.verticalSpeed) // 3s to impact
					{
						flightCamera.transform.position += (35f - cameraRadarAltitude) * cameraUp;
						cameraRadarAltitude = GetRadarAltitudeAtPos(flightCamera.transform.position);
					}

					// Correct for being below terrain/water (min of 30m AGL).
					if (cameraRadarAltitude < 30f)
					{
						flightCamera.transform.position += (30f - cameraRadarAltitude) * cameraUp;
					}
					if (vessel.radarAltitude > 0f) // Make sure terrain isn't in the way (as long as the target is above ground).
					{
						int count = 0;
						Ray ray;
						RaycastHit hit;
						do
						{
							ray = new Ray(flightCamera.transform.position, vessel.transform.position - flightCamera.transform.position);
							if (Physics.Raycast(ray, out hit, (flightCamera.transform.position - vessel.transform.position).magnitude, 1 << 15)) // Just terrain.
							{
								flightCamera.transform.position += 50f * cameraUp; // Try 50m higher.
							}
							else
							{
								break;
							} // We're clear.
						} while (hit.collider != null && ++count < 100); // Max 5km higher.
					}
				}
				else if (manualOffset)
				{
					setPresetOffset = false;
					float sideDistance = manualOffsetRight;
					float distanceAhead = manualOffsetForward;

					if (vessel.Velocity().sqrMagnitude > 1)
					{ flightCamera.transform.position = vessel.transform.position + distanceAhead * vessel.Velocity().normalized; }
					else
					{ flightCamera.transform.position = vessel.transform.position + distanceAhead * vessel.vesselTransform.up; }

					if (flightCamera.mode == FlightCamera.Modes.FREE || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.FREE)
					{
						flightCamera.transform.position += (sideDistance * rightAxis) + (manualOffsetUp * cameraUp);
					}
					else if (flightCamera.mode == FlightCamera.Modes.ORBITAL || FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL)
					{
						flightCamera.transform.position += (sideDistance * FlightGlobals.getUpAxis()) + (manualOffsetUp * Vector3.up);
					}
				}
				else if (setPresetOffset)
				{
					flightCamera.transform.position = presetOffset;
					//setPresetOffset = false;
				}

				// Camera rotation.
				if (hasTarget)
				{ flightCamera.transform.rotation = Quaternion.LookRotation(vessel.transform.position - flightCamera.transform.position, cameraUp); }

				// Initial velocity
				initialVelocity = vessel.Velocity();
				initialOrbit = new Orbit(vessel.orbit);

				cameraToolActive = true;

				ResetDoppler();
				if (OnResetCTools != null)
				{ OnResetCTools(); }
				SetDoppler(true);
				AddAtmoAudioControllers(true);
			}
			else
			{
				Debug.Log("[CameraTools]: Stationary Camera failed. Active Vessel is null.");
			}
			if (hasSavedRotation) { flightCamera.transform.rotation = savedRotation; }
		}

		Vector3 lastOffset = Vector3.zero;
		void UpdateStationaryCamera()
		{
			if (useAudioEffects)
			{
				speedOfSound = 233 * Math.Sqrt(1 + (FlightGlobals.getExternalTemperature(vessel.GetWorldPos3D(), vessel.mainBody) / 273.15));
				//Debug.Log("[CameraTools]: speed of sound: " + speedOfSound);
			}

			if (flightCamera.Target != null) flightCamera.SetTargetNone(); //dont go to next vessel if vessel is destroyed

			// Set camera position before rotation to avoid jitter.
			if (vessel != null)
			{
				var offsetSinceLastFrame = vessel.CoM - lastVesselCoM;
				lastVesselCoM = vessel.CoM;
				cameraParent.transform.position = manualPosition + vessel.CoM;
				if (!randomMode && vessel.srfSpeed > maxRelV / 2 && offsetSinceLastFrame.sqrMagnitude > signedMaxRelVSqr * TimeWarp.fixedDeltaTime * TimeWarp.fixedDeltaTime) // Account for maxRelV. Note: we use fixedDeltaTime here as we're interested in how far it jumped on the physics update. Also check for srfSpeed to account for changes in CoM when on launchpad (srfSpeed < maxRelV/2 should be good for maxRelV down to around 1 in most cases). Also, ignore this when using random mode.
				{
					offsetSinceLastFrame = maxRelV * TimeWarp.fixedDeltaTime * offsetSinceLastFrame.normalized;
				}
				flightCamera.transform.position -= offsetSinceLastFrame;
			}

			// Set camera rotation.
			if (camTarget != null)
			{
				Vector3 lookPosition = camTarget.transform.position;
				if (targetCoM)
				{
					lookPosition = camTarget.vessel.CoM;
				}

				flightCamera.transform.rotation = Quaternion.LookRotation(lookPosition - flightCamera.transform.position, cameraUp);
				lastTargetPosition = lookPosition;
			}
			else if (hasTarget)
			{
				flightCamera.transform.rotation = Quaternion.LookRotation(lastTargetPosition - flightCamera.transform.position, cameraUp);
			}

			if (DEBUG2)
			{
				debug2Messages.Clear();
				Debug2Log("warp mode: " + TimeWarp.WarpMode + ", warp factor: " + TimeWarp.CurrentRate);
				Debug2Log("situation: " + vessel.situation);
				Debug2Log("vessel velocity: " + vessel.Velocity().ToString("G3") + ", Kraken velocity: " + Krakensbane.GetFrameVelocity().ToString("G3") + ", ΔKv: " + Krakensbane.GetLastCorrection().ToString("G3"));
				Debug2Log("floating origin offset: " + FloatingOrigin.Offset.ToString("G3") + ", offsetNonKB: " + FloatingOrigin.OffsetNonKrakensbane.ToString("G3"));
				Debug2Log("floatingKrakenAdjustment: " + floatingKrakenAdjustment.ToString("G3"));
				Debug2Log("vessel - camera offset: " + (vessel.transform.position - flightCamera.transform.position).ToString("G3"));
				Debug2Log("Δoffset: " + (lastOffset - (vessel.transform.position - flightCamera.transform.position)).ToString("G3"));
				lastOffset = vessel.transform.position - flightCamera.transform.position;
			}

			//mouse panning, moving
			forwardAxis = (Quaternion.AngleAxis(-90, cameraUp) * flightCamera.transform.right).normalized; // FIXME Why are these calculated like this? They ought to either use pre-existing axes or cross products.
			rightAxis = (Quaternion.AngleAxis(90, forwardAxis) * cameraUp).normalized;

			//free move
			if (enableKeypad && !boundThisFrame)
			{
				if (Input.GetKey(fmUpKey))
				{
					manualPosition += cameraUp * freeMoveSpeed * Time.fixedDeltaTime;
				}
				else if (Input.GetKey(fmDownKey))
				{
					manualPosition -= cameraUp * freeMoveSpeed * Time.fixedDeltaTime;
				}
				if (Input.GetKey(fmForwardKey))
				{
					manualPosition += forwardAxis * freeMoveSpeed * Time.fixedDeltaTime;
				}
				else if (Input.GetKey(fmBackKey))
				{
					manualPosition -= forwardAxis * freeMoveSpeed * Time.fixedDeltaTime;
				}
				if (Input.GetKey(fmLeftKey))
				{
					manualPosition -= flightCamera.transform.right * freeMoveSpeed * Time.fixedDeltaTime;
				}
				else if (Input.GetKey(fmRightKey))
				{
					manualPosition += flightCamera.transform.right * freeMoveSpeed * Time.fixedDeltaTime;
				}

				//keyZoom
				if (!autoFOV)
				{
					if (Input.GetKey(fmZoomInKey))
					{
						zoomExp = Mathf.Clamp(zoomExp + (keyZoomSpeed * Time.fixedDeltaTime), 1, 8);
					}
					else if (Input.GetKey(fmZoomOutKey))
					{
						zoomExp = Mathf.Clamp(zoomExp - (keyZoomSpeed * Time.fixedDeltaTime), 1, 8);
					}
				}
				else
				{
					if (Input.GetKey(fmZoomInKey))
					{
						autoZoomMargin = Mathf.Clamp(autoZoomMargin + (keyZoomSpeed * 10 * Time.fixedDeltaTime), 0, 50);
					}
					else if (Input.GetKey(fmZoomOutKey))
					{
						autoZoomMargin = Mathf.Clamp(autoZoomMargin - (keyZoomSpeed * 10 * Time.fixedDeltaTime), 0, 50);
					}
				}
			}

			if (camTarget == null && Input.GetKey(KeyCode.Mouse1))
			{
				flightCamera.transform.rotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse X") * 1.7f, Vector3.up); //*(Mathf.Abs(Mouse.delta.x)/7)
				flightCamera.transform.rotation *= Quaternion.AngleAxis(-Input.GetAxis("Mouse Y") * 1.7f, Vector3.right);
				flightCamera.transform.rotation = Quaternion.LookRotation(flightCamera.transform.forward, cameraUp);
			}
			if (Input.GetKey(KeyCode.Mouse2))
			{
				manualPosition += flightCamera.transform.right * Input.GetAxis("Mouse X") * 2;
				manualPosition += forwardAxis * Input.GetAxis("Mouse Y") * 2;
			}
			manualPosition += cameraUp * 10 * Input.GetAxis("Mouse ScrollWheel");

			//autoFov
			if (camTarget != null && autoFOV)
			{
				float cameraDistance = Vector3.Distance(camTarget.transform.position, flightCamera.transform.position);
				float targetFoV = Mathf.Clamp((7000 / (cameraDistance + 100)) - 14 + autoZoomMargin, 2, 60);
				//flightCamera.SetFoV(targetFoV);	
				manualFOV = targetFoV;
			}
			//FOV
			if (!autoFOV)
			{
				zoomFactor = Mathf.Exp(zoomExp) / Mathf.Exp(1);
				manualFOV = 60 / zoomFactor;
				updateFOV = (currentFOV != manualFOV);
				if (updateFOV)
				{
					currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
					flightCamera.SetFoV(currentFOV);
					updateFOV = false;
				}
			}
			else
			{
				currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
				flightCamera.SetFoV(currentFOV);
				zoomFactor = 60 / currentFOV;
			}

			//vessel camera shake
			if (shakeMultiplier > 0)
			{
				foreach (var v in FlightGlobals.Vessels)
				{
					if (!v || !v.loaded || v.packed) continue;
					VesselCameraShake(v);
				}
			}
		}
		#endregion

		#region Pathing Camera
		void StartPathingCam()
		{
			toolMode = ToolModes.Pathing;
			if (selectedPathIndex < 0 || currentPath.keyframeCount <= 0)
			{
				if (DEBUG) Debug.Log("[CameraTools]: Unable to start pathing camera due to no valid paths.");
				RevertCamera();
				return;
			}
			if (FlightGlobals.ActiveVessel == null)
			{
				Debug.LogWarning("[CameraTools]: Unable to start pathing camera due to no active vessel.");
				RevertCamera();
				return;
			}
			if (DEBUG)
			{
				message = "Starting pathing camera.";
				Debug.Log("[CameraTools]: " + message);
				DebugLog(message);
			}
			hasDied = false;
			vessel = FlightGlobals.ActiveVessel;
			cameraUp = -FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()).normalized;
			if (FlightCamera.fetch.mode == FlightCamera.Modes.ORBITAL || (FlightCamera.fetch.mode == FlightCamera.Modes.AUTO && FlightCamera.GetAutoModeForVessel(vessel) == FlightCamera.Modes.ORBITAL))
			{
				cameraUp = Vector3.up;
			}
			pathingLerpRate = Mathf.Pow(10, -2f * currentPath.secondarySmoothing);

			SetCameraParent(vessel.transform);
			cameraToolActive = true;
		}

		void UpdatePathingCam()
		{
			cameraParent.transform.position = vessel.transform.position;
			cameraParent.transform.rotation = vessel.transform.rotation;

			if (isPlayingPath)
			{
				CameraTransformation tf = currentPath.Evaulate(pathTime * currentPath.timeScale);
				flightCamera.transform.localPosition = Vector3.Lerp(flightCamera.transform.localPosition, tf.position, pathingLerpRate);
				flightCamera.transform.localRotation = Quaternion.Slerp(flightCamera.transform.localRotation, tf.rotation, pathingLerpRate);
				zoomExp = Mathf.Lerp(zoomExp, tf.zoom, pathingLerpRate);

			}
			else
			{
				//move
				//mouse panning, moving
				if (Input.GetKey(fmMovementModifier))
				{
					// Note: The forwardAxis and rightAxis are reversed as this is more convenient when viewing the vessel from the front (which is a more typical use-case).
					upAxis = -vessel.ReferenceTransform.forward;
					forwardAxis = -vessel.ReferenceTransform.up;
					rightAxis = -vessel.ReferenceTransform.right;
				}
				else
				{
					upAxis = flightCamera.transform.up;
					forwardAxis = flightCamera.transform.forward;
					rightAxis = flightCamera.transform.right;
				}

				if (enableKeypad && !boundThisFrame)
				{
					if (Input.GetKey(fmUpKey))
					{
						flightCamera.transform.position += upAxis * freeMoveSpeed * Time.fixedDeltaTime;
					}
					else if (Input.GetKey(fmDownKey))
					{
						flightCamera.transform.position -= upAxis * freeMoveSpeed * Time.fixedDeltaTime;
					}
					if (Input.GetKey(fmForwardKey))
					{
						flightCamera.transform.position += forwardAxis * freeMoveSpeed * Time.fixedDeltaTime;
					}
					else if (Input.GetKey(fmBackKey))
					{
						flightCamera.transform.position -= forwardAxis * freeMoveSpeed * Time.fixedDeltaTime;
					}
					if (Input.GetKey(fmLeftKey))
					{
						flightCamera.transform.position -= rightAxis * freeMoveSpeed * Time.fixedDeltaTime;
					}
					else if (Input.GetKey(fmRightKey))
					{
						flightCamera.transform.position += rightAxis * freeMoveSpeed * Time.fixedDeltaTime;
					}

					//keyZoom Note: pathing doesn't use autoZoomMargin
					if (Input.GetKey(fmZoomInKey))
					{
						zoomExp = Mathf.Clamp(zoomExp + (keyZoomSpeed * Time.fixedDeltaTime), 1, 8);
					}
					else if (Input.GetKey(fmZoomOutKey))
					{
						zoomExp = Mathf.Clamp(zoomExp - (keyZoomSpeed * Time.fixedDeltaTime), 1, 8);
					}
				}

				if (Input.GetKey(KeyCode.Mouse1) && Input.GetKey(KeyCode.Mouse2)) // Middle & right: tilt left/right
				{
					flightCamera.transform.rotation = Quaternion.AngleAxis(Input.GetAxis("Mouse X") * -1.7f, flightCamera.transform.forward) * flightCamera.transform.rotation;
				}
				else if (Input.GetKey(KeyCode.Mouse0) && Input.GetKey(KeyCode.Mouse2)) // Left & middle: move up/down
				{
					flightCamera.transform.position += upAxis * Input.GetAxis("Mouse Y") * 2;
				}
				else
				{
					if (Input.GetKey(KeyCode.Mouse1)) // Right: rotate around target
					{
						flightCamera.transform.rotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse X") * 1.7f / (zoomExp * zoomExp), Vector3.up); //*(Mathf.Abs(Mouse.delta.x)/7)
						flightCamera.transform.rotation *= Quaternion.AngleAxis(-Input.GetAxis("Mouse Y") * 1.7f / (zoomExp * zoomExp), Vector3.right);
						flightCamera.transform.rotation = Quaternion.LookRotation(flightCamera.transform.forward, flightCamera.transform.up);
					}
					if (Input.GetKey(KeyCode.Mouse2)) // Middle: move left/right and forward/backward
					{
						flightCamera.transform.position += rightAxis * Input.GetAxis("Mouse X") * 2;
						flightCamera.transform.position += forwardAxis * Input.GetAxis("Mouse Y") * 2;
					}
				}
				if (freeMoveSpeedRaw != (freeMoveSpeedRaw = Mathf.Clamp(freeMoveSpeedRaw + 0.5f * Input.GetAxis("Mouse ScrollWheel"), freeMoveSpeedMinRaw, freeMoveSpeedMaxRaw)))
				{
					freeMoveSpeed = Mathf.Pow(10f, freeMoveSpeedRaw);
				}
			}

			//zoom
			zoomFactor = Mathf.Exp(zoomExp) / Mathf.Exp(1);
			manualFOV = 60 / zoomFactor;
			updateFOV = (currentFOV != manualFOV);
			if (updateFOV)
			{
				currentFOV = Mathf.Lerp(currentFOV, manualFOV, 0.1f);
				flightCamera.SetFoV(currentFOV);
				updateFOV = false;
			}
		}

		void CreateNewPath()
		{
			showKeyframeEditor = false;
			availablePaths.Add(new CameraPath());
			selectedPathIndex = availablePaths.Count - 1;
			if (isPlayingPath) StopPlayingPath();
		}

		void DeletePath(int index)
		{
			if (index < 0) return;
			if (index >= availablePaths.Count) return;
			availablePaths.RemoveAt(index);
			if (index <= selectedPathIndex) { --selectedPathIndex; }
			if (isPlayingPath) StopPlayingPath();
		}

		void SelectPath(int index)
		{
			selectedPathIndex = index;
		}

		void SelectKeyframe(int index)
		{
			if (isPlayingPath)
			{
				StopPlayingPath();
			}
			currentKeyframeIndex = index;
			UpdateCurrentValues();
			showKeyframeEditor = true;
			ViewKeyframe(currentKeyframeIndex);
		}

		void DeselectKeyframe()
		{
			currentKeyframeIndex = -1;
			showKeyframeEditor = false;
		}

		void DeleteKeyframe(int index)
		{
			currentPath.RemoveKeyframe(index);
			if (index == currentKeyframeIndex)
			{
				DeselectKeyframe();
			}
			if (currentPath.keyframeCount > 0 && currentKeyframeIndex >= 0)
			{
				SelectKeyframe(Mathf.Clamp(currentKeyframeIndex, 0, currentPath.keyframeCount - 1));
			}
			else
			{
				if (isPlayingPath) StopPlayingPath();
			}
		}

		void UpdateCurrentValues()
		{
			if (currentPath == null) return;
			if (currentKeyframeIndex < 0 || currentKeyframeIndex >= currentPath.keyframeCount)
			{
				return;
			}
			CameraKeyframe currentKey = currentPath.GetKeyframe(currentKeyframeIndex);
			currentKeyframeTime = currentKey.time;
			currentKeyframePositionInterpolationType = currentKey.positionInterpolationType;
			currentKeyframeRotationInterpolationType = currentKey.rotationInterpolationType;

			currKeyTimeString = currentKeyframeTime.ToString();
		}

		void CreateNewKeyframe()
		{
			if (FlightGlobals.ActiveVessel == null)
			{
				Debug.LogWarning("[CameraTools]: Unable to create new pathing keyframe without an active vessel.");
			}
			showPathSelectorWindow = false;

			float time = 0;
			PositionInterpolationType positionInterpolationType = PositionInterpolationType.CubicSpline;
			RotationInterpolationType rotationInterpolationType = RotationInterpolationType.CubicSpline;
			if (currentPath.keyframeCount > 0)
			{
				CameraKeyframe previousKeyframe = currentPath.GetKeyframe(currentPath.keyframeCount - 1);
				positionInterpolationType = previousKeyframe.positionInterpolationType;
				rotationInterpolationType = previousKeyframe.rotationInterpolationType;

				if (isPlayingPath)
				{
					time = pathTime * currentPath.timeScale;
				}
				else
				{
					time = previousKeyframe.time + 10;
				}
			}

			if (!cameraToolActive)
			{
				if (flightCamera.FieldOfView != flightCamera.fovDefault)
				{
					zoomFactor = 60 / flightCamera.FieldOfView;
					zoomExp = Mathf.Log(zoomFactor) + 1f;
				}

				if (!cameraParentWasStolen)
					SaveOriginalCamera();
				SetCameraParent(FlightGlobals.ActiveVessel.transform);
				cameraToolActive = true;
			}

			currentPath.AddTransform(flightCamera.transform, zoomExp, time, positionInterpolationType, rotationInterpolationType);

			SelectKeyframe(currentPath.times.IndexOf(time));

			if (currentPath.keyframeCount > 6)
			{
				keysScrollPos.y += entryHeight;
			}
		}

		void ViewKeyframe(int index)
		{
			if (!cameraToolActive)
			{
				StartPathingCam();
			}
			CameraKeyframe currentKey = currentPath.GetKeyframe(index);
			flightCamera.transform.localPosition = currentKey.position;
			flightCamera.transform.localRotation = currentKey.rotation;
			SetZoomImmediate(currentKey.zoom);
		}

		void PlayPathingCam()
		{
			if (DEBUG)
			{
				message = "Playing pathing camera.";
				Debug.Log("[CameraTools]: " + message);
				DebugLog(message);
			}
			if (selectedPathIndex < 0)
			{
				if (DEBUG) Debug.Log("[CameraTools]: selectedPathIndex < 0, reverting.");
				RevertCamera();
				return;
			}

			if (currentPath.keyframeCount <= 0)
			{
				if (DEBUG) Debug.Log("[CameraTools]: keyframeCount <= 0, reverting.");
				RevertCamera();
				return;
			}

			float startTime = 0;
			if (currentKeyframeIndex > -1)
			{
				startTime = currentPath.GetKeyframe(currentKeyframeIndex).time;
			}

			DeselectKeyframe();

			if (!cameraToolActive)
			{
				StartPathingCam();
			}

			CameraTransformation firstFrame = currentPath.Evaulate(startTime);
			flightCamera.transform.localPosition = firstFrame.position;
			flightCamera.transform.localRotation = firstFrame.rotation;
			SetZoomImmediate(firstFrame.zoom);

			isPlayingPath = true;
			pathStartTime = GetTime() - (startTime / currentPath.timeScale);
		}

		void StopPlayingPath()
		{
			isPlayingPath = false;
		}

		void TogglePathList()
		{
			showKeyframeEditor = false;
			showPathSelectorWindow = !showPathSelectorWindow;
		}
		#endregion

		#region Shake
		public void ShakeCamera(float magnitude)
		{
			shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
		}

		void UpdateCameraShake()
		{
			if (shakeMultiplier > 0)
			{
				if (shakeMagnitude > 0.1f)
				{
					Vector3 shakeAxis = UnityEngine.Random.onUnitSphere;
					shakeOffset = Mathf.Sin(shakeMagnitude * 20 * Time.time) * (shakeMagnitude / 10) * shakeAxis;
				}


				flightCamera.transform.rotation = Quaternion.AngleAxis((shakeMultiplier / 2) * shakeMagnitude / 50f, Vector3.ProjectOnPlane(UnityEngine.Random.onUnitSphere, flightCamera.transform.forward)) * flightCamera.transform.rotation;
			}

			shakeMagnitude = Mathf.Lerp(shakeMagnitude, 0, 0.1f);
		}

		public void VesselCameraShake(Vessel vessel)
		{
			if (vessel.vesselType == VesselType.Debris) return; // Ignore debris

			//shake
			float camDistance = Vector3.Distance(flightCamera.transform.position, vessel.CoM);

			float distanceFactor = 50f / camDistance;
			float fovFactor = 2f / zoomFactor;
			float thrustFactor = GetTotalThrust() / 1000f;

			float atmosphericFactor = (float)vessel.dynamicPressurekPa / 2f;

			float angleToCam = Vector3.Angle(vessel.srf_velocity, FlightCamera.fetch.mainCamera.transform.position - vessel.transform.position);
			angleToCam = Mathf.Clamp(angleToCam, 1, 180);

			float srfSpeed = (float)vessel.srfSpeed;

			float lagAudioFactor = (75000 / (Vector3.Distance(vessel.transform.position, FlightCamera.fetch.mainCamera.transform.position) * srfSpeed * angleToCam / 90));
			lagAudioFactor = Mathf.Clamp(lagAudioFactor * lagAudioFactor * lagAudioFactor, 0, 4);
			lagAudioFactor += srfSpeed / 230;

			float waveFrontFactor = ((3.67f * angleToCam) / srfSpeed);
			waveFrontFactor = Mathf.Clamp(waveFrontFactor * waveFrontFactor * waveFrontFactor, 0, 2);
			if (vessel.srfSpeed > 330)
			{
				waveFrontFactor = (srfSpeed / (angleToCam) < 3.67f) ? srfSpeed / 15 : 0;
			}

			lagAudioFactor *= waveFrontFactor;

			lagAudioFactor = Mathf.Clamp01(lagAudioFactor) * distanceFactor * fovFactor;

			atmosphericFactor *= lagAudioFactor;

			thrustFactor *= distanceFactor * fovFactor * lagAudioFactor;

			ShakeCamera(atmosphericFactor + thrustFactor);
		}

		float GetTotalThrust()
		{
			float total = 0;
			using (var engine = engines.GetEnumerator())
				while (engine.MoveNext())
				{
					if (engine.Current == null) continue;
					total += engine.Current.finalThrust;
				}
			return total;
		}
		#endregion

		#region Atmospherics
		void AddAtmoAudioControllers(bool includeActiveVessel)
		{
			if (!useAudioEffects)
			{
				return;
			}

			foreach (var vessel in FlightGlobals.Vessels)
			{
				if (!vessel || !vessel.loaded || vessel.packed || (!includeActiveVessel && vessel.isActiveVessel))
				{
					continue;
				}
				if (ignoreVesselTypesForAudio.Contains(vessel.vesselType)) continue;

				if (vessel.gameObject.GetComponent<CTAtmosphericAudioController>() == null)
				{ vessel.gameObject.AddComponent<CTAtmosphericAudioController>(); }
			}
		}

		void SetDoppler(bool includeActiveVessel)
		{
			if (hasSetDoppler)
			{
				return;
			}

			if (!useAudioEffects)
			{
				return;
			}

			// Debug.Log($"DEBUG Setting doppler");
			// Debug.Log($"DEBUG audio spatializer: {AudioSettings.GetSpatializerPluginName()}"); This is an empty string, so doppler effects using Unity's built-in settings are not available.
			// Manually handling doppler effects won't work either as there's no events for newly added audioSources and no way to check when the pitch is adjusted for other reasons.

			audioSources = FindObjectsOfType<AudioSource>();
			originalAudioSourceDoppler = new float[audioSources.Length];
			// Debug.Log($"DEBUG AudioSource pitch: "+ string.Join(", ", audioSources.Where(a => a.isPlaying).Select(a => $"{a.name}: {a.pitch}")));

			for (int i = 0; i < audioSources.Length; i++)
			{
				// Debug.Log("CameraTools.DEBUG audioSources: " + string.Join(", ", audioSources.Select(a => a.name)));
				if (excludeAudioSources.Contains(audioSources[i].name)) continue;
				originalAudioSourceDoppler[i] = audioSources[i].dopplerLevel;

				if (!includeActiveVessel)
				{
					Part p = audioSources[i].GetComponentInParent<Part>();
					if (p && p.vessel.isActiveVessel) continue;
				}

				audioSources[i].dopplerLevel = 1;
				audioSources[i].velocityUpdateMode = AudioVelocityUpdateMode.Fixed;
				audioSources[i].bypassEffects = false;
				audioSources[i].spatialBlend = 1;
				audioSources[i].spatialize = true;

				var part = audioSources[i].gameObject.GetComponentInParent<Part>();
				if (part != null && part.vessel != null && !ignoreVesselTypesForAudio.Contains(part.vessel.vesselType))
				{
					CTPartAudioController pa = audioSources[i].gameObject.GetComponent<CTPartAudioController>();
					if (pa == null) pa = audioSources[i].gameObject.AddComponent<CTPartAudioController>();
					pa.audioSource = audioSources[i];
					// if (audioSources[i].isPlaying) Debug.Log($"DEBUG adding part audio controller for {part} on {part.vessel.vesselName} for audiosource {i} ({audioSources[i].name}) with priority: {audioSources[i].priority}, doppler level {audioSources[i].dopplerLevel}, rollOff: {audioSources[i].rolloffMode}, spatialize: {audioSources[i].spatialize}, spatial blend: {audioSources[i].spatialBlend}, min/max dist:{audioSources[i].minDistance}/{audioSources[i].maxDistance}, clip: {audioSources[i].clip?.name}, output group: {audioSources[i].outputAudioMixerGroup}");
				}
			}

			hasSetDoppler = true;
		}

		void ResetDoppler()
		{
			if (!hasSetDoppler)
			{
				return;
			}

			for (int i = 0; i < audioSources.Length; i++)
			{
				if (audioSources[i] == null || excludeAudioSources.Contains(audioSources[i].name)) continue;
				audioSources[i].dopplerLevel = originalAudioSourceDoppler[i];
				audioSources[i].velocityUpdateMode = AudioVelocityUpdateMode.Auto;
			}

			hasSetDoppler = false;
		}
		#endregion

		#region Revert/Reset
		void SwitchToVessel(Vessel v)
		{
			if (v == null)
			{
				RevertCamera();
				return;
			}
			if (DEBUG)
			{
				message = "Switching to vessel " + v.vesselName;
				Debug.Log("[CameraTools]: " + message);
				DebugLog(message);
			}
			vessel = v;
			BDAIFieldsNeedUpdating = true;
			// Switch to a usable camera mode if necessary.
			if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
			{
				CameraManager.Instance.SetCameraFlight();
			}
			cockpitView = false;
			cockpits.Clear();
			engines.Clear();

			CheckForBDAI(v);
			if (cameraToolActive)
			{
				if (hasBDAI && useBDAutoTarget)
					CheckForBDWM(v);
				UpdateAIDogfightTarget();

				if (randomMode)
				{
					var lowAlt = Math.Max(30d, -3d * vessel.verticalSpeed); // 30m or 3s to impact, whichever is higher.
					var stationarySurfaceVessel = (vessel.Landed && vessel.Speed() < 1) || (vessel.Splashed && vessel.Speed() < 5); // Land or water vessel that isn't moving much.
					if (stationarySurfaceVessel || (hasPilotAI && vessel.radarAltitude < lowAlt))
					{
						StartStationaryCamera();
					}
					else
					{
						ChooseRandomMode();
						// Actual switching is delayed until the LateUpdate to avoid a flicker.
					}
				}

				engines = vessel.FindPartModulesImplementing<ModuleEngines>();
				vesselSwitched = true;
			}
		}

		void ChooseRandomMode()
		{
			cockpits.Clear();
			var rand = rng.Next(100);
			if (rand < randomModeDogfightChance)
			{
				toolMode = ToolModes.DogfightCamera;
			}
			else if (rand < randomModeDogfightChance + randomModeIVAChance)
			{
				toolMode = ToolModes.DogfightCamera;
				cockpits = vessel.FindPartModulesImplementing<ModuleCommand>();
				if (cockpits.Any(c => c.part.protoModuleCrew.Count > 0))
				{ cockpitView = true; }
			}
			else if (rand < randomModeDogfightChance + randomModeIVAChance + randomModeStationaryChance)
			{
				toolMode = ToolModes.StationaryCamera;
			}
			else
			{
				toolMode = ToolModes.Pathing;
			}
		}

		public void RevertCamera()
		{
			if (DEBUG)
			{
				message = "Reverting camera.";
				Debug.Log("[CameraTools]: " + message);
				DebugLog(message);
			}
			if (CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.Flight)
			{
				CameraManager.Instance.SetCameraFlight();
				cameraToolActive = true;
			}

			if (cameraToolActive)
			{
				presetOffset = flightCamera.transform.position;
				if (camTarget == null && saveRotation)
				{
					savedRotation = flightCamera.transform.rotation;
					hasSavedRotation = true;
				}
				else
				{
					hasSavedRotation = false;
				}
			}
			hasDied = false;
			if (FlightGlobals.ActiveVessel != null && HighLogic.LoadedScene == GameScenes.FLIGHT && flightCamera.vesselTarget != FlightGlobals.ActiveVessel)
			{
				flightCamera.SetTarget(FlightGlobals.ActiveVessel.transform, FlightCamera.TargetMode.Vessel);
			}
			if (cameraToolActive)
			{
				flightCamera.transform.parent = origParent;
				if (origParent != null) // Restore the camera to the original local offsets from the original gameObject.
				{
					flightCamera.transform.localPosition = origLocalPosition;
					flightCamera.transform.localRotation = origLocalRotation;
					flightCamera.SetDistanceImmediate(origDistance);
				}
				else // Otherwise, restore the camera to the original absolute position and rotation as the original gameObject no longer exists (if it even existed to begin with).
				{
					flightCamera.transform.position = origPosition;
					flightCamera.transform.rotation = origRotation;
				}
				flightCamera.mode = origMode; // Restore the camera mode. Note: using flightCamera.setModeImmediate(origMode); causes the annoying camera mode change messages to appear, simply setting the value doesn't do this and seems to work fine.
				flightCamera.SetFoV(origFov);
				currentFOV = origFov;
				cameraParentWasStolen = false;
			}
			if (HighLogic.LoadedSceneIsFlight)
				flightCamera.mainCamera.nearClipPlane = origNearClip;
			else
				Camera.main.nearClipPlane = origNearClip;

			flightCamera.ActivateUpdate();

			cameraToolActive = false;


			StopPlayingPath();

			ResetDoppler();

			try
			{
				if (OnResetCTools != null)
				{ OnResetCTools(); }
			}
			catch (Exception e)
			{ Debug.Log("[CameraTools]: Caught exception resetting CameraTools " + e.ToString()); }
		}

		void SaveOriginalCamera()
		{
			origPosition = flightCamera.transform.position;
			origRotation = flightCamera.transform.rotation;
			origLocalPosition = flightCamera.transform.localPosition;
			origLocalRotation = flightCamera.transform.localRotation;
			origParent = flightCamera.transform.parent;
			origNearClip = HighLogic.LoadedSceneIsFlight ? flightCamera.mainCamera.nearClipPlane : Camera.main.nearClipPlane;
			origDistance = flightCamera.Distance;
			origMode = flightCamera.mode;
			origFov = flightCamera.FieldOfView;
		}

		void PostDeathRevert()
		{
			if (cameraToolActive)
			{
				RevertCamera();
			}
		}

		void PostDeathRevert(GameScenes f)
		{
			if (cameraToolActive)
			{
				RevertCamera();
			}
		}

		void PostDeathRevert(Vessel v)
		{
			if (cameraToolActive)
			{
				RevertCamera();
			}
		}
		#endregion

		#region GUI
		//GUI
		void OnGUI()
		{
			if (guiEnabled && gameUIToggle && HighLogic.LoadedSceneIsFlight)
			{
				if (inputFieldStyle == null) SetupInputFieldStyle();
				windowRect = GUI.Window(320, windowRect, GuiWindow, "");

				if (showKeyframeEditor)
				{
					KeyframeEditorWindow();
				}
				if (showPathSelectorWindow)
				{
					PathSelectorWindow();
				}
			}
			if (DEBUG)
			{
				if (debugMessages.Count > 0)
				{
					var now = Time.time;
					debugMessages = debugMessages.Where(m => now - m.Item1 < 5f).ToList();
					if (debugMessages.Count > 0)
					{
						var messages = string.Join("\n", debugMessages.Select(m => m.Item1.ToString("0.000") + " " + m.Item2));
						GUI.Label(cShadowRect, messages, cShadowStyle);
						GUI.Label(cDebugRect, messages, cStyle);
					}
				}
			}
			if (DEBUG2)
			{
				if (debug2Messages.Count > 0)
				{
					GUI.Label(new Rect(Screen.width - 750, 100, 700, 400), string.Join("\n", debug2Messages.Select(m => m.Item1.ToString("0.000") + " " + m.Item2)));
				}
			}
		}

		Rect LabelRect(float line)
		{ return new Rect(leftIndent, contentTop + line * entryHeight, contentWidth, entryHeight); }
		Rect LeftRect(float line)
		{ return new Rect(leftIndent, contentTop + line * entryHeight, windowWidth / 2f + leftIndent * 2f, entryHeight); }
		Rect RightRect(float line)
		{ return new Rect(windowWidth / 2f + 3f * leftIndent, contentTop + line * entryHeight, contentWidth / 2f - 3f * leftIndent, entryHeight); }
		Rect QuarterRect(float line, int quarter)
		{ return new Rect(leftIndent + quarter * contentWidth / 4, contentTop + line * entryHeight, contentWidth / 4, entryHeight); }
		void SetupInputFieldStyle()
		{
			inputFieldStyle = new GUIStyle(GUI.skin.textField);
			inputFieldStyle.alignment = TextAnchor.UpperRight;
		}
		void GuiWindow(int windowID)
		{
			GUI.DragWindow(new Rect(0, 0, windowWidth, draggableHeight));

			GUI.Label(new Rect(0, contentTop, windowWidth, 40), "Camera Tools", titleStyle);
			GUI.Label(new Rect(windowWidth / 2f, contentTop + 35f, windowWidth / 2f - leftIndent - entryHeight, entryHeight), $"Version: {Version}", watermarkStyle);
			if (GUI.Toggle(new Rect(windowWidth - leftIndent - 14f, contentTop + 31f, 20f, 20f), cameraToolActive, "") != cameraToolActive)
			{
				if (cameraToolActive)
				{
					autoEnableOverriden = true;
					RevertCamera();
				}
				else
				{
					autoEnableOverriden = false;
					if (randomMode)
					{
						ChooseRandomMode();
					}
					cameraActivate();
				}
			}

			float line = 1.75f;
			float parseResult;

			//tool mode switcher
			GUI.Label(LabelRect(++line), "Tool: " + toolMode.ToString(), leftLabelBold);
			if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), 25, entryHeight - 2), "<"))
			{
				CycleToolMode(false);
				if (cameraToolActive) cameraActivate();
			}
			if (GUI.Button(new Rect(leftIndent + 25 + 4, contentTop + (line * entryHeight), 25, entryHeight - 2), ">"))
			{
				CycleToolMode(true);
				if (cameraToolActive) cameraActivate();
			}
			if (GUI.Button(new Rect(windowWidth - leftIndent - 25, contentTop + (line * entryHeight), 25, entryHeight - 2), "#", textInput ? GUI.skin.box : GUI.skin.button))
			{
				textInput = !textInput;
				if (!textInput) // Set the fields to their currently showing values.
				{
					foreach (var field in inputFields.Keys)
					{
						inputFields[field].tryParseValueNow();
						var fieldInfo = typeof(CamTools).GetField(field);
						if (fieldInfo != null) { fieldInfo.SetValue(this, inputFields[field].currentValue); }
						else
						{
							var propInfo = typeof(CamTools).GetProperty(field);
							propInfo.SetValue(this, inputFields[field].currentValue);
						}
					}
					if (currentPath != null)
					{
						currentPath.secondarySmoothing = pathingSecondarySmoothing;
						currentPath.timeScale = pathingTimeScale;
					}
					freeMoveSpeedRaw = Mathf.Log10(freeMoveSpeed);
					zoomSpeedRaw = Mathf.Log10(keyZoomSpeed);
				}
				else // Set the input fields to their current values.
				{
					if (currentPath != null)
					{
						pathingSecondarySmoothing = currentPath.secondarySmoothing;
						pathingTimeScale = currentPath.timeScale;
					}
					foreach (var field in inputFields.Keys)
					{
						var fieldInfo = typeof(CamTools).GetField(field);
						if (fieldInfo != null) { inputFields[field].currentValue = (float)fieldInfo.GetValue(this); }
						else
						{
							var propInfo = typeof(CamTools).GetProperty(field);
							inputFields[field].currentValue = (float)propInfo.GetValue(this);
						}
					}

				}
			}
			line++;

			useAudioEffects = GUI.Toggle(LabelRect(++line), useAudioEffects, "Use Audio Effects");
			autoEnableForBDA = GUI.Toggle(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), autoEnableForBDA, "Auto-Enable for BDArmory");

			line++;
			if (autoFOV && toolMode != ToolModes.Pathing)
			{
				GUI.Label(LeftRect(++line), "Autozoom Margin: ");
				if (!textInput)
				{
					autoZoomMargin = GUI.HorizontalSlider(new Rect(leftIndent, contentTop + ((++line) * entryHeight), contentWidth - 45, entryHeight), autoZoomMargin, 0, 50);
					if (!enableKeypad) autoZoomMargin = Mathf.RoundToInt(autoZoomMargin * 2f) / 2f;
					GUI.Label(new Rect(leftIndent + contentWidth - 40, contentTop + ((line - 0.15f) * entryHeight), 40, entryHeight), autoZoomMargin.ToString("G3"), leftLabel);
				}
				else
				{
					inputFields["autoZoomMargin"].tryParseValue(GUI.TextField(RightRect(line), inputFields["autoZoomMargin"].possibleValue, 8, inputFieldStyle));
					autoZoomMargin = inputFields["autoZoomMargin"].currentValue;
				}
			}
			else
			{
				GUI.Label(LeftRect(++line), "Zoom:", leftLabel);
				if (!textInput)
				{
					zoomExp = GUI.HorizontalSlider(new Rect(leftIndent, contentTop + ((++line) * entryHeight), contentWidth - 45, entryHeight), zoomExp, 1, 8);
					GUI.Label(new Rect(leftIndent + contentWidth - 40, contentTop + ((line - 0.15f) * entryHeight), 40, entryHeight), zoomFactor.ToString("G3") + "x", leftLabel);
				}
				else
				{
					inputFields["zoomFactor"].tryParseValue(GUI.TextField(RightRect(line), inputFields["zoomFactor"].possibleValue, 8, inputFieldStyle));
					zoomExp = Mathf.Log(inputFields["zoomFactor"].currentValue) + 1f;
				}
			}
			if (toolMode != ToolModes.Pathing)
			{
				autoFOV = GUI.Toggle(LabelRect(++line), autoFOV, "Auto Zoom");
			}

			++line;
			GUI.Label(LeftRect(++line), "Camera Shake:");
			if (!textInput)
			{
				shakeMultiplier = Mathf.Round(GUI.HorizontalSlider(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth - 45, entryHeight), shakeMultiplier, 0f, 10f) * 10f) / 10f;
				GUI.Label(new Rect(leftIndent + contentWidth - 40, contentTop + ((line - 0.25f) * entryHeight), 40, entryHeight), shakeMultiplier.ToString("G3") + "x");
			}
			else
			{
				inputFields["shakeMultiplier"].tryParseValue(GUI.TextField(RightRect(line), inputFields["shakeMultiplier"].possibleValue, 8, inputFieldStyle));
				shakeMultiplier = inputFields["shakeMultiplier"].currentValue;
			}
			line++;

			//Stationary camera GUI
			if (toolMode == ToolModes.StationaryCamera)
			{
				GUI.Label(LeftRect(++line), "Max Relative Vel.: ", leftLabel);
				inputFields["maxRelV"].tryParseValue(GUI.TextField(RightRect(line), inputFields["maxRelV"].possibleValue, 12, inputFieldStyle));
				maxRelV = inputFields["maxRelV"].currentValue;
				signedMaxRelVSqr = Mathf.Abs(maxRelV) * maxRelV;

				maintainInitialVelocity = GUI.Toggle(LeftRect(++line), maintainInitialVelocity, "Maintain Vel.");
				if (maintainInitialVelocity) useOrbital = GUI.Toggle(RightRect(line), useOrbital, "Use Orbital");

				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), "Camera Position:", leftLabel);
				string posButtonText = "Set Position w/ Click";
				if (setPresetOffset) posButtonText = "Clear Position";
				if (waitingForPosition) posButtonText = "Waiting...";
				if (FlightGlobals.ActiveVessel != null && GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight - 2), posButtonText))
				{
					if (setPresetOffset)
					{
						setPresetOffset = false;
					}
					else
					{
						waitingForPosition = true;
						mouseUp = false;
					}
				}
				autoFlybyPosition = GUI.Toggle(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), autoFlybyPosition, "Auto Flyby Position");
				if (autoFlybyPosition) manualOffset = false;
				manualOffset = GUI.Toggle(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), manualOffset, "Manual Flyby Position");
				Color origGuiColor = GUI.color;
				if (manualOffset)
				{ autoFlybyPosition = false; }
				else
				{ GUI.color = new Color(0.5f, 0.5f, 0.5f, origGuiColor.a); }

				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), 60, entryHeight), "Fwd:", leftLabel);
				float textFieldWidth = 42;
				Rect fwdFieldRect = new Rect(leftIndent + contentWidth - textFieldWidth - (3 * incrButtonWidth), contentTop + (line * entryHeight), textFieldWidth, entryHeight);
				guiOffsetForward = GUI.TextField(fwdFieldRect, guiOffsetForward.ToString());
				if (float.TryParse(guiOffsetForward, out parseResult))
				{
					manualOffsetForward = parseResult;
				}
				DrawIncrementButtons(fwdFieldRect, ref manualOffsetForward);
				guiOffsetForward = manualOffsetForward.ToString();

				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), 60, entryHeight), "Right:", leftLabel);
				Rect rightFieldRect = new Rect(fwdFieldRect.x, contentTop + (line * entryHeight), textFieldWidth, entryHeight);
				guiOffsetRight = GUI.TextField(rightFieldRect, guiOffsetRight);
				if (float.TryParse(guiOffsetRight, out parseResult))
				{
					manualOffsetRight = parseResult;
				}
				DrawIncrementButtons(rightFieldRect, ref manualOffsetRight);
				guiOffsetRight = manualOffsetRight.ToString();

				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), 60, entryHeight), "Up:", leftLabel);
				Rect upFieldRect = new Rect(fwdFieldRect.x, contentTop + (line * entryHeight), textFieldWidth, entryHeight);
				guiOffsetUp = GUI.TextField(upFieldRect, guiOffsetUp);
				if (float.TryParse(guiOffsetUp, out parseResult))
				{ manualOffsetUp = parseResult; }
				DrawIncrementButtons(upFieldRect, ref manualOffsetUp);
				guiOffsetUp = manualOffsetUp.ToString();
				GUI.color = origGuiColor;
				line++;

				string targetText = "None";
				if (camTarget != null) targetText = camTarget.gameObject.name;
				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), "Camera Target: " + targetText, leftLabel);
				string tgtButtonText = "Set Target w/ Click";
				if (waitingForTarget) tgtButtonText = "waiting...";
				if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight - 2), tgtButtonText))
				{
					waitingForTarget = true;
					mouseUp = false;
				}
				if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), (contentWidth / 2) - 2, entryHeight - 2), "Target Self"))
				{
					camTarget = FlightGlobals.ActiveVessel.GetReferenceTransformPart();
					hasTarget = true;
				}
				if (GUI.Button(new Rect(2 + leftIndent + contentWidth / 2, contentTop + (line * entryHeight), (contentWidth / 2) - 2, entryHeight - 2), "Clear Target"))
				{
					camTarget = null;
					hasTarget = false;
				}
				targetCoM = GUI.Toggle(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight - 2), targetCoM, "Vessel Center of Mass");
				if (camTarget == null) saveRotation = GUI.Toggle(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight - 2), saveRotation, "Save Rotation");
				if (!saveRotation) hasSavedRotation = false;
			}
			else if (toolMode == ToolModes.DogfightCamera)
			{
				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), "Secondary Target:");
				string tVesselLabel;
				if (showingVesselList)
				{ tVesselLabel = "Clear"; }
				else if (dogfightTarget)
				{ tVesselLabel = dogfightTarget.vesselName; }
				else
				{ tVesselLabel = "None"; }
				if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), tVesselLabel))
				{
					if (showingVesselList)
					{
						showingVesselList = false;
						dogfightTarget = null;
					}
					else
					{
						UpdateLoadedVessels();
						showingVesselList = true;
					}
				}
				if (showingVesselList)
				{
					foreach (var v in loadedVessels)
					{
						if (!v || !v.loaded) continue;
						if (GUI.Button(new Rect(leftIndent + 10f, contentTop + (++line * entryHeight), contentWidth - 10f, entryHeight), v.vesselName))
						{
							dogfightTarget = v;
							showingVesselList = false;
						}
					}
				}
				if (hasBDAI)
				{
					useBDAutoTarget = GUI.Toggle(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight - 2), useBDAutoTarget, "BDA AI Auto target");
				}
				line++;

				GUI.Label(LeftRect(++line), "Distance: " + dogfightDistance.ToString("G3"));
				if (!textInput)
				{
					line += 0.15f;
					dogfightDistance = GUI.HorizontalSlider(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), dogfightDistance, 1f, 100f);
					if (!enableKeypad) dogfightDistance = Mathf.RoundToInt(dogfightDistance * 2f) / 2f;
				}
				else
				{
					inputFields["dogfightDistance"].tryParseValue(GUI.TextField(RightRect(line), inputFields["dogfightDistance"].possibleValue, 8, inputFieldStyle));
					dogfightDistance = inputFields["dogfightDistance"].currentValue;
				}

				GUI.Label(LeftRect(++line), "Offset:");
				if (!textInput)
				{
					GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), 15f, entryHeight), "X: ");
					dogfightOffsetX = GUI.HorizontalSlider(new Rect(leftIndent + 15f, contentTop + (line * entryHeight) + 6f, contentWidth - 45f, entryHeight), dogfightOffsetX, -dogfightMaxOffset, dogfightMaxOffset);
					if (!enableKeypad) dogfightOffsetX = Mathf.RoundToInt(dogfightOffsetX * 2f) / 2f;
					GUI.Label(new Rect(leftIndent + contentWidth - 25f, contentTop + (line * entryHeight), 25f, entryHeight), dogfightOffsetX.ToString("G3"));
					GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), 15, entryHeight), "Y: ");
					dogfightOffsetY = GUI.HorizontalSlider(new Rect(leftIndent + 15f, contentTop + (line * entryHeight) + 6f, contentWidth - 45f, entryHeight), dogfightOffsetY, -dogfightMaxOffset, dogfightMaxOffset);
					if (!enableKeypad) dogfightOffsetY = Mathf.RoundToInt(dogfightOffsetY * 2f) / 2f;
					GUI.Label(new Rect(leftIndent + contentWidth - 25f, contentTop + (line * entryHeight), 25, entryHeight), dogfightOffsetY.ToString("G3"));
					line += 0.5f;

					GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), 30f, entryHeight), "Lerp: ");
					dogfightLerp = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(leftIndent + 30f, contentTop + (line * entryHeight) + 6f, contentWidth - 60f, entryHeight), dogfightLerp * 100f, 1f, 50f)) / 100f;
					GUI.Label(new Rect(leftIndent + contentWidth - 25f, contentTop + (line * entryHeight), 25f, entryHeight), dogfightLerp.ToString("G3"));
					GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), 30f, entryHeight), "Roll: ");
					dogfightRoll = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(leftIndent + 30f, contentTop + (line * entryHeight) + 6f, contentWidth - 60f, entryHeight), dogfightRoll * 20f, 0f, 20f)) / 20f;
					GUI.Label(new Rect(leftIndent + contentWidth - 25f, contentTop + (line * entryHeight), 25f, entryHeight), dogfightRoll.ToString("G3"));
					line += 0.15f;
				}
				else
				{
					GUI.Label(QuarterRect(++line, 0), "X: ", rightLabel);
					inputFields["dogfightOffsetX"].tryParseValue(GUI.TextField(QuarterRect(line, 1), inputFields["dogfightOffsetX"].possibleValue, 8, inputFieldStyle));
					dogfightOffsetX = inputFields["dogfightOffsetX"].currentValue;
					GUI.Label(QuarterRect(line, 2), "Y: ", rightLabel);
					inputFields["dogfightOffsetY"].tryParseValue(GUI.TextField(QuarterRect(line, 3), inputFields["dogfightOffsetY"].possibleValue, 8, inputFieldStyle));
					dogfightOffsetY = inputFields["dogfightOffsetY"].currentValue;
					GUI.Label(QuarterRect(++line, 0), "Lerp: ", rightLabel);
					inputFields["dogfightLerp"].tryParseValue(GUI.TextField(QuarterRect(line, 1), inputFields["dogfightLerp"].possibleValue, 8, inputFieldStyle));
					dogfightLerp = inputFields["dogfightLerp"].currentValue;
					GUI.Label(QuarterRect(line, 2), "Roll: ", rightLabel);
					inputFields["dogfightRoll"].tryParseValue(GUI.TextField(QuarterRect(line, 3), inputFields["dogfightRoll"].possibleValue, 8, inputFieldStyle));
					dogfightRoll = inputFields["dogfightRoll"].currentValue;
				}
			}
			else if (toolMode == ToolModes.Pathing)
			{
				if (selectedPathIndex >= 0)
				{
					GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), "Path:");
					currentPath.pathName = GUI.TextField(new Rect(leftIndent + 34, contentTop + (line * entryHeight), contentWidth - 34, entryHeight), currentPath.pathName);
				}
				else
				{ GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), "Path: None"); }
				line += 0.25f;
				if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), "Open Path"))
				{ TogglePathList(); }
				if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth / 2f, entryHeight), "New Path"))
				{ CreateNewPath(); }
				if (GUI.Button(new Rect(leftIndent + (contentWidth / 2f), contentTop + (line * entryHeight), contentWidth / 2f, entryHeight), "Delete Path"))
				{ DeletePath(selectedPathIndex); }
				line += 0.25f;

				if (selectedPathIndex >= 0)
				{
					if (!textInput)
					{
						GUI.Label(LabelRect(++line), "Secondary Smoothing: " + currentPath.secondarySmoothing.ToString("G2"));
						if (currentPath.secondarySmoothing != (currentPath.secondarySmoothing = Mathf.Round(GUI.HorizontalSlider(new Rect(leftIndent, contentTop + (++line * entryHeight) + 4f, contentWidth, entryHeight), currentPath.secondarySmoothing, 0f, 1f) * 100f) / 100f))
						{ pathingLerpRate = Mathf.Pow(10, -2f * currentPath.secondarySmoothing); }
					}
					else
					{
						GUI.Label(LeftRect(++line), "Secondary Smoothing:");
						inputFields["pathingSecondarySmoothing"].tryParseValue(GUI.TextField(RightRect(line), inputFields["pathingSecondarySmoothing"].possibleValue, 8, inputFieldStyle));
						if (currentPath.secondarySmoothing != (currentPath.secondarySmoothing = inputFields["pathingSecondarySmoothing"].currentValue))
						{ pathingLerpRate = Mathf.Pow(10, -2f * currentPath.secondarySmoothing); }
					}
					if (!textInput)
					{
						GUI.Label(LabelRect(++line), "Path Timescale: " + currentPath.timeScale.ToString("G3"));
						currentPath.timeScale = GUI.HorizontalSlider(new Rect(leftIndent, contentTop + (++line * entryHeight) + 4f, contentWidth, entryHeight), currentPath.timeScale, 0.05f, 4f);
						currentPath.timeScale = Mathf.Round(currentPath.timeScale * 20f) / 20f;
					}
					else
					{
						GUI.Label(LeftRect(++line), "Path Timescale:");
						inputFields["pathingTimeScale"].tryParseValue(GUI.TextField(RightRect(line), inputFields["pathingTimeScale"].possibleValue, 8, inputFieldStyle));
						currentPath.timeScale = inputFields["pathingTimeScale"].currentValue;
					}
					if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), useRealTime ? "Real-time" : "In-Game time"))
					{
						useRealTime = !useRealTime;
					}
					++line;
					float viewHeight = Mathf.Max(6f * entryHeight, currentPath.keyframeCount * entryHeight);
					Rect scrollRect = new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, 6 * entryHeight);
					GUI.Box(scrollRect, string.Empty);
					float viewContentWidth = contentWidth - (2f * leftIndent);
					keysScrollPos = GUI.BeginScrollView(scrollRect, keysScrollPos, new Rect(0f, 0f, viewContentWidth, viewHeight));
					if (currentPath.keyframeCount > 0)
					{
						Color origGuiColor = GUI.color;
						for (int i = 0; i < currentPath.keyframeCount; ++i)
						{
							if (i == currentKeyframeIndex)
							{
								GUI.color = Color.green;
							}
							else
							{
								GUI.color = origGuiColor;
							}
							string kLabel = "#" + i.ToString() + ": " + currentPath.GetKeyframe(i).time.ToString("G3") + "s";
							if (GUI.Button(new Rect(0f, (i * entryHeight), 3f * viewContentWidth / 4f, entryHeight), kLabel))
							{
								SelectKeyframe(i);
							}
							if (GUI.Button(new Rect((3f * contentWidth / 4f), (i * entryHeight), (viewContentWidth / 4f) - 20f, entryHeight), "X"))
							{
								DeleteKeyframe(i);
								break;
							}
						}
						GUI.color = origGuiColor;
					}
					GUI.EndScrollView();
					line += 5.25f;
					if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), 3f * contentWidth / 4f, entryHeight), "New Key"))
					{ CreateNewKeyframe(); }
				}
			}
			line += 0.25f;

			randomMode = GUI.Toggle(LabelRect(++line), randomMode, "Random Mode");
			if (randomMode)
			{
				float oldValue = randomModeDogfightChance;
				if (!textInput)
				{
					GUI.Label(LeftRect(++line), $"Dogfight ({randomModeDogfightChance:F0}%)");
					randomModeDogfightChance = GUI.HorizontalSlider(new Rect(leftIndent + contentWidth / 2f, contentTop + (line * entryHeight) + 6, contentWidth / 2f, entryHeight), randomModeDogfightChance, 0f, 100f);
				}
				else
				{
					GUI.Label(LeftRect(++line), "Dogfight %: ");
					inputFields["randomModeDogfightChance"].tryParseValue(GUI.TextField(RightRect(line), inputFields["randomModeDogfightChance"].possibleValue, 8, inputFieldStyle));
					randomModeDogfightChance = inputFields["randomModeDogfightChance"].currentValue;
				}
				if (oldValue != randomModeDogfightChance)
				{
					var remainder = 100f - randomModeDogfightChance;
					var total = randomModeIVAChance + randomModeStationaryChance + randomModePathingChance;
					if (total > 0f)
					{
						randomModeIVAChance = Mathf.Round(remainder * randomModeIVAChance / total);
						randomModeStationaryChance = Mathf.Round(remainder * randomModeStationaryChance / total);
						randomModePathingChance = Mathf.Round(remainder * randomModePathingChance / total);
					}
					else
					{
						randomModeIVAChance = Mathf.Round(remainder / 3f);
						randomModeStationaryChance = Mathf.Round(remainder / 3f);
						randomModePathingChance = Mathf.Round(remainder / 3f);
					}
					randomModeDogfightChance = 100f - randomModeIVAChance - randomModeStationaryChance - randomModePathingChance; // Any rounding errors go to the adjusted slider.
					inputFields["randomModeDogfightChance"].currentValue = randomModeDogfightChance;
					inputFields["randomModeIVAChance"].currentValue = randomModeIVAChance;
					inputFields["randomModeStationaryChance"].currentValue = randomModeStationaryChance;
					inputFields["randomModePathingChance"].currentValue = randomModePathingChance;
				}

				oldValue = randomModeIVAChance;
				if (!textInput)
				{
					GUI.Label(LeftRect(++line), $"IVA ({randomModeIVAChance:F0}%)");
					randomModeIVAChance = GUI.HorizontalSlider(new Rect(leftIndent + contentWidth / 2f, contentTop + (line * entryHeight) + 6f, contentWidth / 2f, entryHeight), randomModeIVAChance, 0f, 100f);
				}
				else
				{
					GUI.Label(LeftRect(++line), "IVA %: ");
					inputFields["randomModeIVAChance"].tryParseValue(GUI.TextField(RightRect(line), inputFields["randomModeIVAChance"].possibleValue, 8, inputFieldStyle));
					randomModeIVAChance = inputFields["randomModeIVAChance"].currentValue;
				}
				if (oldValue != randomModeIVAChance)
				{
					var remainder = 100f - randomModeIVAChance;
					var total = randomModeDogfightChance + randomModeStationaryChance + randomModePathingChance;
					if (total > 0f)
					{
						randomModeDogfightChance = Mathf.Round(remainder * randomModeDogfightChance / total);
						randomModeStationaryChance = Mathf.Round(remainder * randomModeStationaryChance / total);
						randomModePathingChance = Mathf.Round(remainder * randomModePathingChance / total);
					}
					else
					{
						randomModeDogfightChance = Mathf.Round(remainder / 3f);
						randomModeStationaryChance = Mathf.Round(remainder / 3f);
						randomModePathingChance = Mathf.Round(remainder / 3f);
					}
					randomModeIVAChance = 100f - randomModeDogfightChance - randomModeStationaryChance - randomModePathingChance; // Any rounding errors go to the adjusted slider.
					inputFields["randomModeDogfightChance"].currentValue = randomModeDogfightChance;
					inputFields["randomModeIVAChance"].currentValue = randomModeIVAChance;
					inputFields["randomModeStationaryChance"].currentValue = randomModeStationaryChance;
					inputFields["randomModePathingChance"].currentValue = randomModePathingChance;
				}

				oldValue = randomModeStationaryChance;
				if (!textInput)
				{
					GUI.Label(LeftRect(++line), $"Stationary ({randomModeStationaryChance:F0}%)");
					randomModeStationaryChance = GUI.HorizontalSlider(new Rect(leftIndent + contentWidth / 2f, contentTop + (line * entryHeight) + 6, contentWidth / 2f, entryHeight), randomModeStationaryChance, 0f, 100f);
				}
				else
				{
					GUI.Label(LeftRect(++line), "Stationary %: ");
					inputFields["randomModeStationaryChance"].tryParseValue(GUI.TextField(RightRect(line), inputFields["randomModeStationaryChance"].possibleValue, 8, inputFieldStyle));
					randomModeStationaryChance = inputFields["randomModeStationaryChance"].currentValue;
				}
				if (oldValue != randomModeStationaryChance)
				{
					var remainder = 100f - randomModeStationaryChance;
					var total = randomModeDogfightChance + randomModeIVAChance + randomModePathingChance;
					if (total > 0)
					{
						randomModeDogfightChance = Mathf.Round(remainder * randomModeDogfightChance / total);
						randomModeIVAChance = Mathf.Round(remainder * randomModeIVAChance / total);
						randomModePathingChance = Mathf.Round(remainder * randomModePathingChance / total);
					}
					else
					{
						randomModeDogfightChance = Mathf.Round(remainder / 3f);
						randomModeIVAChance = Mathf.Round(remainder / 3f);
						randomModePathingChance = Mathf.Round(remainder / 3f);
					}
					randomModeStationaryChance = 100f - randomModeDogfightChance - randomModeIVAChance - randomModePathingChance; // Any rounding errors go to the adjusted slider.
					inputFields["randomModeDogfightChance"].currentValue = randomModeDogfightChance;
					inputFields["randomModeIVAChance"].currentValue = randomModeIVAChance;
					inputFields["randomModeStationaryChance"].currentValue = randomModeStationaryChance;
					inputFields["randomModePathingChance"].currentValue = randomModePathingChance;
				}

				oldValue = randomModePathingChance;
				if (!textInput)
				{
					GUI.Label(LeftRect(++line), $"Pathing ({randomModePathingChance:F0}%)");
					randomModePathingChance = GUI.HorizontalSlider(new Rect(leftIndent + contentWidth / 2f, contentTop + (line * entryHeight) + 6f, contentWidth / 2f, entryHeight), randomModePathingChance, 0f, 100f);
				}
				else
				{
					GUI.Label(LeftRect(++line), "Pathing %: ");
					inputFields["randomModePathingChance"].tryParseValue(GUI.TextField(RightRect(line), inputFields["randomModePathingChance"].possibleValue, 8, inputFieldStyle));
					randomModePathingChance = inputFields["randomModePathingChance"].currentValue;
				}
				if (oldValue != randomModePathingChance)
				{
					var remainder = 100f - randomModePathingChance;
					var total = randomModeDogfightChance + randomModeIVAChance + randomModeStationaryChance;
					if (total > 0)
					{
						randomModeDogfightChance = Mathf.Round(remainder * randomModeDogfightChance / total);
						randomModeIVAChance = Mathf.Round(remainder * randomModeIVAChance / total);
						randomModeStationaryChance = Mathf.Round(remainder * randomModeStationaryChance / total);
					}
					else
					{
						randomModeDogfightChance = Mathf.Round(remainder / 3f);
						randomModeIVAChance = Mathf.Round(remainder / 3f);
						randomModeStationaryChance = Mathf.Round(remainder / 3f);
					}
					randomModePathingChance = 100f - randomModeDogfightChance - randomModeIVAChance - randomModeStationaryChance; // Any rounding errors go to the adjusted slider.
					inputFields["randomModeDogfightChance"].currentValue = randomModeDogfightChance;
					inputFields["randomModeIVAChance"].currentValue = randomModeIVAChance;
					inputFields["randomModeStationaryChance"].currentValue = randomModeStationaryChance;
					inputFields["randomModePathingChance"].currentValue = randomModePathingChance;
				}
			}

			line += 0.25f;
			enableKeypad = GUI.Toggle(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), enableKeypad, "Keypad Control");
			if (enableKeypad)
			{
				GUI.Label(LeftRect(++line), "Move Speed:");
				if (!textInput)
				{
					freeMoveSpeedRaw = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(leftIndent + contentWidth / 2f - 30, contentTop + (line * entryHeight) + 6f, contentWidth / 2f, entryHeight), freeMoveSpeedRaw, freeMoveSpeedMinRaw, freeMoveSpeedMaxRaw) * 100f) / 100f;
					freeMoveSpeed = Mathf.Pow(10f, freeMoveSpeedRaw);
					GUI.Label(new Rect(leftIndent + contentWidth - 25f, contentTop + (line * entryHeight), 25f, entryHeight), freeMoveSpeed.ToString("G3"));
				}
				else
				{
					inputFields["freeMoveSpeed"].tryParseValue(GUI.TextField(RightRect(line), inputFields["freeMoveSpeed"].possibleValue, 8, inputFieldStyle));
					freeMoveSpeed = inputFields["freeMoveSpeed"].currentValue;
				}

				GUI.Label(LeftRect(++line), "Zoom Speed:");
				if (!textInput)
				{
					zoomSpeedRaw = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(leftIndent + contentWidth / 2f - 30f, contentTop + (line * entryHeight) + 6f, contentWidth / 2f, entryHeight), zoomSpeedRaw, zoomSpeedMinRaw, zoomSpeedMaxRaw) * 100f) / 100f;
					keyZoomSpeed = Mathf.Pow(10f, zoomSpeedRaw);
					GUI.Label(new Rect(leftIndent + contentWidth - 25f, contentTop + (line * entryHeight), 25f, entryHeight), keyZoomSpeed.ToString("G3"));
				}
				else
				{
					inputFields["keyZoomSpeed"].tryParseValue(GUI.TextField(RightRect(line), inputFields["keyZoomSpeed"].possibleValue, 8, inputFieldStyle));
					keyZoomSpeed = inputFields["keyZoomSpeed"].currentValue;
				}
			}
			line++;

			// Key bindings
			if (GUI.Button(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), "Edit Keybindings"))
			{ editingKeybindings = !editingKeybindings; }
			if (editingKeybindings)
			{
				cameraKey = KeyBinding(cameraKey, "Activate", ++line);
				revertKey = KeyBinding(revertKey, "Revert", ++line);
				toggleMenu = KeyBinding(toggleMenu, "Menu", ++line);
				fmUpKey = KeyBinding(fmUpKey, "Up", ++line);
				fmDownKey = KeyBinding(fmDownKey, "Down", ++line);
				fmForwardKey = KeyBinding(fmForwardKey, "Forward", ++line);
				fmBackKey = KeyBinding(fmBackKey, "Back", ++line);
				fmLeftKey = KeyBinding(fmLeftKey, "Left", ++line);
				fmRightKey = KeyBinding(fmRightKey, "Right", ++line);
				fmZoomInKey = KeyBinding(fmZoomInKey, "Zoom In", ++line);
				fmZoomOutKey = KeyBinding(fmZoomOutKey, "Zoom Out", ++line);
				fmMovementModifier = KeyBinding(fmMovementModifier, "Modifier", ++line);
			}

			Rect saveRect = new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth / 2, entryHeight);
			if (GUI.Button(saveRect, "Save"))
			{ Save(); }

			Rect loadRect = new Rect(saveRect);
			loadRect.x += contentWidth / 2;
			if (GUI.Button(loadRect, "Reload"))
			{
				if (isPlayingPath) StopPlayingPath();
				Load();
			}

			float timeSinceLastSaved = Time.unscaledTime - lastSavedTime;
			if (timeSinceLastSaved < 1)
			{
				++line;
				GUI.Label(new Rect(leftIndent, contentTop + (++line * entryHeight), contentWidth, entryHeight), timeSinceLastSaved < 0.5 ? "Saving..." : "Saved.", centerLabel);
			}

			//fix length
			windowHeight = contentTop + (line * entryHeight) + entryHeight + entryHeight;
			windowRect.height = windowHeight;// = new Rect(windowRect.x, windowRect.y, windowWidth, windowHeight);
		}

		string KeyBinding(string current, string label, float line)
		{
			GUI.Label(new Rect(leftIndent, contentTop + (line * entryHeight), contentWidth, entryHeight), $"{label}: ", leftLabel);
			GUI.Label(new Rect(leftIndent + 70, contentTop + (line * entryHeight), 50, entryHeight), current, leftLabel);
			if (!isRecordingInput || label != currentlyBinding)
			{
				if (GUI.Button(new Rect(leftIndent + 130, contentTop + (line * entryHeight), 95, entryHeight), "Bind Key"))
				{
					mouseUp = false;
					isRecordingInput = true;
					currentlyBinding = label;
				}
			}
			else if (mouseUp)
			{
				GUI.Label(new Rect(leftIndent + 140, contentTop + (line * entryHeight), 85, entryHeight), "Press a Key", leftLabel);

				string inputString = CCInputUtils.GetInputString();
				if (inputString.Length > 0)
				{
					isRecordingInput = false;
					boundThisFrame = true;
					currentlyBinding = "";
					if (inputString != "escape") // Allow escape key to cancel keybinding.
					{ return inputString; }
				}
			}

			return current;
		}

		void KeyframeEditorWindow()
		{
			float width = 300f;
			float gap = 5;
			float lineHeight = 25f;
			float line = 0f;
			Rect kWindowRect = new Rect(windowRect.x - width, windowRect.y + 365, width, keyframeEditorWindowHeight);
			GUI.Box(kWindowRect, string.Empty);
			GUI.BeginGroup(kWindowRect);
			GUI.Label(new Rect(gap, gap, 100, lineHeight - gap), "Keyframe #" + currentKeyframeIndex);
			if (GUI.Button(new Rect(100 + gap, gap, 200 - 2 * gap, lineHeight), "Revert Pos"))
			{
				ViewKeyframe(currentKeyframeIndex);
			}
			GUI.Label(new Rect(gap, gap + (++line * lineHeight), 80, lineHeight - gap), "Time: ");
			currKeyTimeString = GUI.TextField(new Rect(100 + gap, gap + line * lineHeight, 200 - 2 * gap, lineHeight - gap), currKeyTimeString, 16);
			float parsed;
			if (float.TryParse(currKeyTimeString, out parsed))
			{
				currentKeyframeTime = parsed;
			}
			if (currentKeyframeIndex > 1)
			{
				if (GUI.Button(new Rect(100 + gap, gap + (++line * lineHeight), 200 - 2 * gap, lineHeight - gap), "Maintain Speed"))
				{
					CameraKeyframe previousKeyframe = currentPath.GetKeyframe(currentKeyframeIndex - 1);
					CameraKeyframe previousPreviousKeyframe = currentPath.GetKeyframe(currentKeyframeIndex - 2);
					float previousKeyframeDistance = Vector3.Distance(previousKeyframe.position, previousPreviousKeyframe.position);
					float previousKeyframeDuration = previousKeyframe.time - previousPreviousKeyframe.time;
					float previousKeyframeSpeed = previousKeyframeDistance / previousKeyframeDuration;
					float currentKeyFrameDistance = Vector3.Distance(flightCamera.transform.localPosition, previousKeyframe.position);
					float adjustedDuration = currentKeyFrameDistance / previousKeyframeSpeed;
					float currentKeyframeDuration = currentKeyframeTime - previousKeyframe.time;
					currentKeyframeTime += adjustedDuration - currentKeyframeDuration;
					currKeyTimeString = currentKeyframeTime.ToString();
				}
			}
			GUI.Label(new Rect(gap, gap + (++line * lineHeight), 100, lineHeight - gap), $"Pos: {currentKeyframePositionInterpolationType}");
			currentKeyframePositionInterpolationType = (PositionInterpolationType)Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(100 + 2 * gap, gap + line * lineHeight, 200 - 3 * gap, lineHeight - gap), (float)currentKeyframePositionInterpolationType, 0, PositionInterpolationTypeMax));
			GUI.Label(new Rect(gap, gap + (++line * lineHeight), 100, lineHeight - gap), $"Rot: {currentKeyframeRotationInterpolationType}");
			currentKeyframeRotationInterpolationType = (RotationInterpolationType)Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(100 + 2 * gap, gap + line * lineHeight, 200 - 3 * gap, lineHeight - gap), (float)currentKeyframeRotationInterpolationType, 0, RotationInterpolationTypeMax));
			bool applied = false;
			if (GUI.Button(new Rect(100 + gap, gap + (++line * lineHeight), 200 - 2 * gap, lineHeight - gap), "Apply"))
			{
				Debug.Log("[CameraTools]: Applying keyframe at time: " + currentKeyframeTime);
				currentPath.SetTransform(currentKeyframeIndex, flightCamera.transform, zoomExp, currentKeyframeTime, currentKeyframePositionInterpolationType, currentKeyframeRotationInterpolationType);
				applied = true;
			}
			if (GUI.Button(new Rect(100 + gap, gap + (++line * lineHeight), 200 - 2 * gap, lineHeight - gap), "Cancel"))
			{
				applied = true;
			}
			GUI.EndGroup();

			if (applied)
			{
				DeselectKeyframe();
			}
			keyframeEditorWindowHeight = 2 * gap + (++line * lineHeight);
		}

		bool showPathSelectorWindow = false;
		Vector2 pathSelectScrollPos;
		void PathSelectorWindow()
		{
			float width = 300;
			float height = 300;
			float indent = 5;
			float scrollRectSize = width - indent - indent;
			Rect pSelectRect = new Rect(windowRect.x - width, windowRect.y + 290, width, height);
			GUI.Box(pSelectRect, string.Empty);
			GUI.BeginGroup(pSelectRect);

			Rect scrollRect = new Rect(indent, indent, scrollRectSize, scrollRectSize);
			float scrollHeight = Mathf.Max(scrollRectSize, entryHeight * availablePaths.Count);
			Rect scrollViewRect = new Rect(0, 0, scrollRectSize - 20, scrollHeight);
			pathSelectScrollPos = GUI.BeginScrollView(scrollRect, pathSelectScrollPos, scrollViewRect);
			bool selected = false;
			for (int i = 0; i < availablePaths.Count; i++)
			{
				if (GUI.Button(new Rect(0, i * entryHeight, scrollRectSize - 90, entryHeight), availablePaths[i].pathName))
				{
					SelectPath(i);
					selected = true;
					if (cameraToolActive && currentPath.keyframeCount > 0) PlayPathingCam();
				}
				if (GUI.Button(new Rect(scrollRectSize - 80, i * entryHeight, 60, entryHeight), "Delete"))
				{
					DeletePath(i);
					break;
				}
			}

			GUI.EndScrollView();

			GUI.EndGroup();
			if (selected)
			{
				showPathSelectorWindow = false;
			}
		}

		void DrawIncrementButtons(Rect fieldRect, ref float val)
		{
			Rect incrButtonRect = new Rect(fieldRect.x - incrButtonWidth, fieldRect.y, incrButtonWidth, entryHeight);
			if (GUI.Button(incrButtonRect, "-"))
			{
				val -= 5;
			}

			incrButtonRect.x -= incrButtonWidth;

			if (GUI.Button(incrButtonRect, "--"))
			{
				val -= 50;
			}

			incrButtonRect.x = fieldRect.x + fieldRect.width;

			if (GUI.Button(incrButtonRect, "+"))
			{
				val += 5;
			}

			incrButtonRect.x += incrButtonWidth;

			if (GUI.Button(incrButtonRect, "++"))
			{
				val += 50;
			}
		}

		//AppLauncherSetup
		void AddToolbarButton()
		{
			if (!hasAddedButton)
			{
				Texture buttonTexture = GameDatabase.Instance.GetTexture("CameraTools/Textures/icon", false);
				ApplicationLauncher.Instance.AddModApplication(ToggleGui, ToggleGui, Dummy, Dummy, Dummy, Dummy, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
				CamTools.hasAddedButton = true;
			}

		}

		void ToggleGui()
		{
			if (guiEnabled)
				DisableGui();
			else
				EnableGui();
		}

		void EnableGui()
		{
			guiEnabled = true;
			// Debug.Log("[CameraTools]: Showing CamTools GUI");
		}

		void DisableGui()
		{
			guiEnabled = false;
			Save();
			// Debug.Log("[CameraTools]: Hiding CamTools GUI");
		}

		void Dummy()
		{ }

		void GameUIEnable()
		{
			gameUIToggle = true;
		}

		void GameUIDisable()
		{
			gameUIToggle = false;
		}

		void CycleToolMode(bool forward)
		{
			var length = System.Enum.GetValues(typeof(ToolModes)).Length;
			if (forward)
			{
				toolMode++;
				if ((int)toolMode == length) toolMode = 0;
			}
			else
			{
				toolMode--;
				if ((int)toolMode == -1) toolMode = (ToolModes)length - 1;
			}
			if (toolMode != ToolModes.Pathing)
			{ DeselectKeyframe(); }
		}
		#endregion

		#region Utils
		void CurrentVesselWillDestroy(Vessel v)
		{
			if (vessel == v && cameraToolActive)
			{
				hasDied = true;
				diedTime = Time.time;

				if (DEBUG)
				{
					message = "Activating death camera.";
					Debug.Log("[CameraTools]: " + message);
					DebugLog(message);
				}
				// Something borks the camera position/rotation when the target/parent is set to none/null. This fixes that.
				deathCamVelocity = (vessel.radarAltitude > 10d ? vessel.srf_velocity : Vector3d.zero) / 2f; // Track the explosion a bit.
				SetDeathCam();
			}
		}

		void VesselPartCountChanged(Vessel v)
		{
			if (vessel == v) { BDAIFieldsNeedUpdating = true; }
		}

		Part GetPartFromMouse()
		{
			Vector3 mouseAim = new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height, 0);
			Ray ray = FlightCamera.fetch.mainCamera.ViewportPointToRay(mouseAim);
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, 10000, 1 << 0))
			{
				Part p = hit.transform.GetComponentInParent<Part>();
				return p;
			}
			else return null;
		}

		Vector3 GetPosFromMouse()
		{
			Vector3 mouseAim = new Vector3(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height, 0);
			Ray ray = FlightCamera.fetch.mainCamera.ViewportPointToRay(mouseAim);
			RaycastHit hit;
			if (Physics.Raycast(ray, out hit, 15000, 557057))
			{
				return hit.point - (10 * ray.direction);
			}
			else return Vector3.zero;
		}

		void UpdateLoadedVessels()
		{
			if (loadedVessels == null)
			{
				loadedVessels = new List<Vessel>();
			}
			else
			{
				loadedVessels.Clear();
			}

			foreach (var v in FlightGlobals.Vessels)
			{
				if (v.loaded && v.vesselType != VesselType.Debris && !v.isActiveVessel)
				{
					loadedVessels.Add(v);
				}
			}
		}

		private void CheckForBDAI(Vessel v)
		{
			hasBDAI = false;
			hasPilotAI = false;
			aiComponent = null;
			if (v)
			{
				foreach (Part p in v.parts)
				{ // We actually want BDGenericAIBase, but we can't use GetComponent(string) to find it, so we look for its subclasses.
					if (p.GetComponent("BDModulePilotAI"))
					{
						hasBDAI = true;
						hasPilotAI = true;
						aiComponent = (object)p.GetComponent("BDModulePilotAI");
						return;
					}
					if (p.GetComponent("BDModuleSurfaceAI"))
					{
						hasBDAI = true;
						hasPilotAI = false;
						aiComponent = (object)p.GetComponent("BDModuleSurfaceAI");
						return;
					}
				}
			}
		}

		private void CheckForBDWM(Vessel v)
		{
			hasBDWM = false;
			wmComponent = null;
			if (v)
			{
				foreach (Part p in v.parts)
				{
					if (p.GetComponent("MissileFire"))
					{
						hasBDWM = true;
						wmComponent = (object)p.GetComponent("MissileFire");
						return;
					}
				}
			}
		}

		private Vessel GetAITargetedVessel()
		{
			if (BDAIFieldsNeedUpdating)
			{
				// Update fields
				bdAiTargetField = GetAITargetField();
				bdWmThreatField = GetThreatField();
				bdWmUnderFireField = GetUnderFireField();
				bdWmUnderAttackField = GetUnderAttackField();
				bdWmMissileField = GetMissileField();
				BDAIFieldsNeedUpdating = false;
			}

			if (hasBDWM && wmComponent != null && bdWmThreatField != null)
			{
				bool underFire = (bool)bdWmUnderFireField.GetValue(wmComponent);
				bool underAttack = (bool)bdWmUnderAttackField.GetValue(wmComponent);

				if (bdWmMissileField != null)
					return (Vessel)bdWmMissileField.GetValue(wmComponent); // Priority 1: incoming missiles.
				if (underFire || underAttack)
					return (Vessel)bdWmThreatField.GetValue(wmComponent); // Priority 2: incoming fire.
			}
			if (hasBDAI && aiComponent != null && bdAiTargetField != null)
				return (Vessel)bdAiTargetField.GetValue(aiComponent); // Priority 3: the current vessel's target.

			return null;
		}

		private Type AIModuleType()
		{
			//Debug.Log("[CameraTools]: loaded assy's: ");
			foreach (var assy in AssemblyLoader.loadedAssemblies)
			{
				//Debug.Log("[CameraTools]: - "+assy.assembly.FullName);
				if (assy.assembly.FullName.Contains("BDArmory"))
				{
					foreach (var t in assy.assembly.GetTypes())
					{
						if (t.Name == "BDGenericAIBase")
						{
							return t;
						}
					}
				}
			}

			return null;
		}

		private Type WeaponManagerType()
		{
			// Debug.Log("[CameraTools]: loaded assy's: ");
			foreach (var assy in AssemblyLoader.loadedAssemblies)
			{
				// Debug.Log("[CameraTools]: - "+assy.assembly.FullName);
				if (assy.assembly.FullName.Contains("BDArmory"))
				{
					foreach (var t in assy.assembly.GetTypes())
					{
						if (t.Name == "MissileFire")
						{
							return t;
						}
					}
				}
			}

			return null;
		}

		private string GetVersion()
		{
			try
			{
				Version = this.GetType().Assembly.GetName().Version.ToString();
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[CameraTools]: Failed to get version string: {e.Message}");
			}
			return Version;
		}

		private void CheckForBDA()
		{
			// This checks for the existence of a BDArmory assembly and picks out the BDACompetitionMode and VesselSpawner singletons.
			int foundCount = 0;
			bdCompetitionType = null;
			bdCompetitionInstance = null;
			bdCompetitionIsActiveField = null;
			bdCompetitionStartingField = null;
			bdVesselSpawnerType = null;
			bdVesselSpawnerInstance = null;
			bdVesselsSpawningField = null;
			bdBDATournamentType = null;
			bdBDATournamentInstance = null;
			foreach (var assy in AssemblyLoader.loadedAssemblies)
			{
				if (assy.assembly.FullName.Contains("BDArmory"))
				{
					foreach (var t in assy.assembly.GetTypes())
					{
						if (t != null)
						{
							switch (t.Name)
							{
								case "BDACompetitionMode":
									bdCompetitionType = t;
									bdCompetitionInstance = FindObjectOfType(bdCompetitionType);
									foreach (var fieldInfo in bdCompetitionType.GetFields(BindingFlags.Public | BindingFlags.Instance))
										if (fieldInfo != null)
										{
											switch (fieldInfo.Name)
											{
												case "competitionStarting":
													bdCompetitionStartingField = fieldInfo;
													++foundCount;
													break;
												case "competitionIsActive":
													bdCompetitionIsActiveField = fieldInfo;
													++foundCount;
													break;
												default:
													break;
											}
										}
									break;
								case "VesselSpawner":
									bdVesselSpawnerType = t;
									bdVesselSpawnerInstance = FindObjectOfType(bdVesselSpawnerType);
									foreach (var fieldInfo in bdVesselSpawnerType.GetFields(BindingFlags.Public | BindingFlags.Instance))
										if (fieldInfo != null && fieldInfo.Name == "vesselsSpawning")
											bdVesselsSpawningField = fieldInfo;
									++foundCount;
									break;
								case "BDATournament":
									bdBDATournamentType = t;
									bdBDATournamentInstance = FindObjectOfType(bdBDATournamentType);
									foreach (var fieldInfo in bdBDATournamentType.GetFields(BindingFlags.Public | BindingFlags.Instance))
										if (fieldInfo != null && fieldInfo.Name == "warpingInProgress")
											bdTournamentWarpInProgressField = fieldInfo;
									++foundCount;
									break;
								default:
									break;
							}
						}
						if (foundCount == 4)
							return;
					}
				}
			}
		}

		private void DisableTimeControlsCameraZoomFix()
		{
			try
			{
				foreach (var assy in AssemblyLoader.loadedAssemblies)
				{
					if (assy.assembly.FullName.Contains("TimeControl"))
					{
						foreach (var type in assy.assembly.GetTypes())
						{
							if (type == null) continue;
							if (type.Name == "GlobalSettings")
							{
								var globalSettingsInstance = FindObjectOfType(type);
								if (globalSettingsInstance != null)
								{
									foreach (var propertyInfo in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
									{
										if (propertyInfo != null && propertyInfo.Name == "CameraZoomFix")
										{
											if ((bool)propertyInfo.GetValue(globalSettingsInstance))
											{
												Debug.LogWarning("[CameraTools]: Setting CameraZoomFix variable in TimeControl.GlobalSettings to false as it breaks CameraTools when running in slow-mo.");
												propertyInfo.SetValue(globalSettingsInstance, false);
											}
										}
									}
								}
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogError($"[CameraTools.CamTools]: Failed to disable CameraZoomFix in TimeControl: {e.Message}");
			}
		}

		private void AutoEnableForBDA()
		{
			try
			{
				if (bdVesselsSpawningField != null && (bool)bdVesselsSpawningField.GetValue(bdVesselSpawnerInstance))
				{
					if (autoEnableOverride)
						return; // Still spawning.
					else
					{
						Debug.Log("[CameraTools]: Deactivating CameraTools while spawning vessels.");
						autoEnableOverride = true;
						RevertCamera();
						return;
					}
				}

				if (bdTournamentWarpInProgressField != null && (bool)bdTournamentWarpInProgressField.GetValue(bdBDATournamentInstance))
				{
					if (autoEnableOverride)
						return; // Still warping.
					else
					{
						Debug.Log("[CameraTools]: Deactivating CameraTools while warping between rounds.");
						autoEnableOverride = true;
						RevertCamera();
						return;
					}
				}

				autoEnableOverride = false;
				if (cameraToolActive) return; // It's already active.

				if (vessel == null || (hasPilotAI && vessel.LandedOrSplashed)) return; // Don't activate for landed/splashed planes.
				if (bdCompetitionStartingField != null && (bool)bdCompetitionStartingField.GetValue(bdCompetitionInstance))
				{
					Debug.Log("[CameraTools]: Activating CameraTools for BDArmory competition as competition is starting.");
					cameraActivate();
					return;
				}
				else if (bdCompetitionIsActiveField != null && (bool)bdCompetitionIsActiveField.GetValue(bdCompetitionInstance))
				{
					if (!(toolMode == ToolModes.DogfightCamera && dogfightTarget == null)) // Don't activate dogfight mode without a target once the competition is active.
					{
						Debug.Log("[CameraTools]: Activating CameraTools for BDArmory competition as competition is active.");
						cameraActivate();
						return;
					}
					else // Try to acquire a valid dogfightTarget so we can re-enable the camera.
					{
						UpdateAIDogfightTarget();
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogError("[CameraTools]: Checking competition state of BDArmory failed: " + e.Message);
				bdCompetitionIsActiveField = null;
				bdCompetitionStartingField = null;
				bdCompetitionInstance = null;
				bdCompetitionType = null;
				bdVesselsSpawningField = null;
				bdVesselSpawnerInstance = null;
				bdVesselSpawnerType = null;
				CheckForBDA();
			}
		}

		private FieldInfo GetThreatField()
		{
			Type wmModType = WeaponManagerType();
			if (wmModType == null) return null;

			FieldInfo[] fields = wmModType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
			//Debug.Log("[CameraTools]: bdai fields: ");
			foreach (var f in fields)
			{
				// Debug.Log("[CameraTools]: - " + f.Name);
				if (f.Name == "incomingThreatVessel")
				{
					return f;
				}
			}

			return null;
		}

		private FieldInfo GetMissileField()
		{
			Type wmModType = WeaponManagerType();
			if (wmModType == null) return null;

			FieldInfo[] fields = wmModType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
			//Debug.Log("[CameraTools]: bdai fields: ");
			foreach (var f in fields)
			{
				// Debug.Log("[CameraTools]: - " + f.Name);
				if (f.Name == "incomingMissileVessel")
				{
					return f;
				}
			}

			return null;
		}

		private FieldInfo GetUnderFireField()
		{
			Type wmModType = WeaponManagerType();
			if (wmModType == null) return null;

			FieldInfo[] fields = wmModType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
			//Debug.Log("[CameraTools]: bdai fields: ");
			foreach (var f in fields)
			{
				//Debug.Log("[CameraTools]: - " + f.Name);
				if (f.Name == "underFire")
				{
					return f;
				}
			}

			return null;
		}

		private FieldInfo GetUnderAttackField()
		{
			Type wmModType = WeaponManagerType();
			if (wmModType == null) return null;

			FieldInfo[] fields = wmModType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
			//Debug.Log("[CameraTools]: bdai fields: ");
			foreach (var f in fields)
			{
				//Debug.Log("[CameraTools]: - " + f.Name);
				if (f.Name == "underAttack")
				{
					return f;
				}
			}

			return null;
		}

		private FieldInfo GetAITargetField()
		{
			Type aiModType = AIModuleType();
			if (aiModType == null) return null;

			FieldInfo[] fields = aiModType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
			//Debug.Log("[CameraTools]: bdai fields: ");
			foreach (var f in fields)
			{
				//Debug.Log("[CameraTools]: - " + f.Name);
				if (f.Name == "targetVessel")
				{
					return f;
				}
			}

			return null;
		}

		public static float GetRadarAltitudeAtPos(Vector3 position)
		{
			var geoCoords = FlightGlobals.currentMainBody.GetLatitudeAndLongitude(position);
			var altitude = FlightGlobals.currentMainBody.GetAltitude(position);
			var terrainAltitude = FlightGlobals.currentMainBody.TerrainAltitude(geoCoords.x, geoCoords.y);
			return (float)(altitude - Math.Max(terrainAltitude, 0));
		}

		public float GetTime()
		{
			return useRealTime ? Time.unscaledTime : Time.time;
		}

		public void SetZoomImmediate(float zoom)
		{
			zoomExp = zoom;
			zoomFactor = Mathf.Exp(zoomExp) / Mathf.Exp(1);
			manualFOV = 60 / zoomFactor;
			currentFOV = manualFOV;
			flightCamera.SetFoV(currentFOV);
		}

		void SetCameraParent(Transform referenceTransform, bool resetToCoM = false)
		{
			cameraParent.transform.position = referenceTransform.position;
			cameraParent.transform.rotation = referenceTransform.rotation;
			flightCamera.SetTargetNone();
			flightCamera.transform.parent = cameraParent.transform;
			cameraParentWasStolen = false;
			flightCamera.DeactivateUpdate();
			if (resetToCoM)
			{
				cameraParent.transform.position = vessel.CoM; // Then adjust the flightCamera for the new parent.
				flightCamera.transform.localPosition = cameraParent.transform.InverseTransformPoint(referenceTransform.position);
				flightCamera.transform.localRotation = Quaternion.identity;
			}
		}

		void SetDeathCam()
		{
			flightCamera.SetTargetNone();
			flightCamera.transform.parent = deathCam.transform;
			cameraParentWasStolen = false;
			flightCamera.DeactivateUpdate();
			flightCamera.transform.localPosition = Vector3.zero;
			flightCamera.transform.localRotation = Quaternion.identity;
		}
		#endregion

		#region Load/Save
		void Save()
		{
			CTPersistantField.Save();

			ConfigNode pathFileNode = ConfigNode.Load(CameraPath.pathSaveURL);

			if (pathFileNode == null)
				pathFileNode = new ConfigNode();

			if (!pathFileNode.HasNode("CAMERAPATHS"))
				pathFileNode.AddNode("CAMERAPATHS");

			ConfigNode pathsNode = pathFileNode.GetNode("CAMERAPATHS");
			pathsNode.RemoveNodes("CAMERAPATH");

			foreach (var path in availablePaths)
			{
				path.Save(pathsNode);
			}
			if (!Directory.GetParent(CameraPath.pathSaveURL).Exists)
			{ Directory.GetParent(CameraPath.pathSaveURL).Create(); }
			var success = pathFileNode.Save(CameraPath.pathSaveURL);
			if (success)
			{
				lastSavedTime = Time.unscaledTime;

				if (File.Exists(CameraPath.oldPathSaveURL))
				{ File.Delete(CameraPath.oldPathSaveURL); } // Remove the old settings if it exists and the new settings were saved.
			}
		}

		void Load()
		{
			CTPersistantField.Load();
			guiOffsetForward = manualOffsetForward.ToString();
			guiOffsetRight = manualOffsetRight.ToString();
			guiOffsetUp = manualOffsetUp.ToString();
			guiKeyZoomSpeed = keyZoomSpeed.ToString();
			guiFreeMoveSpeed = freeMoveSpeed.ToString();

			DeselectKeyframe();
			availablePaths = new List<CameraPath>();
			ConfigNode pathFileNode = ConfigNode.Load(CameraPath.pathSaveURL);
			if (pathFileNode == null)
			{
				pathFileNode = ConfigNode.Load(CameraPath.oldPathSaveURL);
			}
			if (pathFileNode != null)
			{
				foreach (var node in pathFileNode.GetNode("CAMERAPATHS").GetNodes("CAMERAPATH"))
				{
					availablePaths.Add(CameraPath.Load(node));
				}
			}
			else
			{
				availablePaths.Add(
					new CameraPath
					{
						pathName = "Example Path",
						points = new List<Vector3> {
							new Vector3(13.40305f, -16.60615f, -4.274539f),
							new Vector3(14.48815f, -13.88801f, -4.26651f),
							new Vector3(14.48839f, -13.88819f, -4.267331f),
							new Vector3(15.52922f, -14.25925f, -4.280066f)
						},
						positionInterpolationTypes = new List<PositionInterpolationType>{
							PositionInterpolationType.CubicSpline,
							PositionInterpolationType.CubicSpline,
							PositionInterpolationType.CubicSpline,
							PositionInterpolationType.CubicSpline
						},
						rotations = new List<Quaternion>{
							new Quaternion( 0.5759971f, 0.2491289f,  -0.2965982f, -0.7198553f),
							new Quaternion(-0.6991884f, 0.09197949f, -0.08556388f, 0.7038141f),
							new Quaternion(-0.6991884f, 0.09197949f, -0.08556388f, 0.7038141f),
							new Quaternion(-0.6506922f, 0.2786613f,  -0.271617f,   0.6520521f)
						},
						rotationInterpolationTypes = new List<RotationInterpolationType>{
							RotationInterpolationType.Slerp,
							RotationInterpolationType.Slerp,
							RotationInterpolationType.Slerp,
							RotationInterpolationType.Slerp
						},
						times = new List<float> { 0f, 1f, 2f, 6f },
						zooms = new List<float> { 1f, 2.035503f, 3.402367f, 3.402367f },
						timeScale = 0.29f
					}
				);
			}
			selectedPathIndex = Math.Min(selectedPathIndex, availablePaths.Count - 1);
			if (availablePaths.Count > 0 && selectedPathIndex < 0) { selectedPathIndex = 0; }
			// Set some internal and GUI variables.
			freeMoveSpeedRaw = Mathf.Log10(freeMoveSpeed);
			freeMoveSpeedMinRaw = Mathf.Log10(freeMoveSpeedMin);
			freeMoveSpeedMaxRaw = Mathf.Log10(freeMoveSpeedMax);
			zoomSpeedRaw = Mathf.Log10(keyZoomSpeed);
			zoomSpeedMinRaw = Mathf.Log10(keyZoomSpeedMin);
			zoomSpeedMaxRaw = Mathf.Log10(keyZoomSpeedMax);
			signedMaxRelVSqr = Mathf.Abs(maxRelV) * maxRelV;
			guiOffsetForward = manualOffsetForward.ToString();
			guiOffsetRight = manualOffsetRight.ToString();
			guiOffsetUp = manualOffsetUp.ToString();
			guiKeyZoomSpeed = keyZoomSpeed.ToString();
			guiFreeMoveSpeed = freeMoveSpeed.ToString();
			if (inputFields != null)
			{
				if (inputFields.ContainsKey("freeMoveSpeed"))
				{ inputFields["freeMoveSpeed"].UpdateLimits(freeMoveSpeedMin, freeMoveSpeedMax); }
				if (inputFields.ContainsKey("keyZoomSpeed"))
				{ inputFields["keyZoomSpeed"].UpdateLimits(keyZoomSpeedMin, keyZoomSpeedMax); }
			}
			if (DEBUG) { Debug.Log("[CameraTools]: Verbose debugging enabled."); }
		}
		#endregion
	}

	public enum ToolModes { StationaryCamera, DogfightCamera, Pathing };
}
