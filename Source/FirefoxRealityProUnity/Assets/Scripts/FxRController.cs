﻿using UnityEngine;
using System;
using System.Collections.Generic;
using Valve.VR;
using VRIME2;

public class FxRController : MonoBehaviour
{
    public enum FXR_LOG_LEVEL
    {
        FXR_LOG_LEVEL_DEBUG = 0,
        FXR_LOG_LEVEL_INFO,
        FXR_LOG_LEVEL_WARN,
        FXR_LOG_LEVEL_ERROR,
        FXR_LOG_LEVEL_REL_INFO
    }

    [SerializeField]
    private FXR_LOG_LEVEL currentLogLevel = FXR_LOG_LEVEL.FXR_LOG_LEVEL_INFO;

    // Main reference to the plugin functions. Created in OnEnable(), destroyed in OnDisable().
    private FxRPlugin fxr_plugin = null;

    public bool DontCloseNativeWindowOnClose = false;

    private List<FxRLaserPointer> LaserPointers
    {
        get
        {
            if (laserPointers == null)
            {
                laserPointers = new List<FxRLaserPointer>();
                laserPointers.AddRange(FindObjectsOfType<FxRLaserPointer>());
            }

            return laserPointers;
        }
    }

    private List<FxRLaserPointer> laserPointers;
    
    //
    // MonoBehavior methods.
    //

    void Awake()
    {
        Debug.Log("FxRController.Awake())");
    }

    [AOT.MonoPInvokeCallback(typeof(FxRPluginLogCallback))]
    public static void Log(System.String msg)
    {
        if (msg.StartsWith("[error]")) Debug.LogError(msg);
        else if (msg.StartsWith("[warning]")) Debug.LogWarning(msg);
        else Debug.Log (msg); // incldues [info] and [debug].
    }

    void OnEnable()
    {
        Debug.Log("FxRController.OnEnable()");

        fxr_plugin = new FxRPlugin();

        Application.runInBackground = true;

        // Register the log callback.
        switch (Application.platform)
        {
            case RuntimePlatform.OSXEditor:                        // Unity Editor on OS X.
            case RuntimePlatform.OSXPlayer:                        // Unity Player on OS X.
            case RuntimePlatform.WindowsEditor:                    // Unity Editor on Windows.
            case RuntimePlatform.WindowsPlayer:                    // Unity Player on Windows.
            case RuntimePlatform.LinuxEditor:
            case RuntimePlatform.LinuxPlayer:
            case RuntimePlatform.WSAPlayerX86:                     // Unity Player on Windows Store X86.
            case RuntimePlatform.WSAPlayerX64:                     // Unity Player on Windows Store X64.
            case RuntimePlatform.WSAPlayerARM:                     // Unity Player on Windows Store ARM.
            case RuntimePlatform.Android:                          // Unity Player on Android.
            case RuntimePlatform.IPhonePlayer:                     // Unity Player on iOS.
                fxr_plugin.fxrRegisterLogCallback(Log);
                break;
            default:
                break;
        }

        // Give the plugin a place to look for resources.
        fxr_plugin.fxrSetResourcesPath(Application.streamingAssetsPath);

        // Set any launch-time parameters.
        if (DontCloseNativeWindowOnClose) fxr_plugin.fxrSetParamBool(FxRPlugin.FxRParam.b_CloseNativeWindowOnClose, false);

        // Set the reference to the plugin in any other objects in the scene that need it.
        FxRWindow[] fxrwindows = FindObjectsOfType<FxRWindow>();
        foreach (FxRWindow w in fxrwindows) {
            w.fxr_plugin = fxr_plugin;
        }
        
        // VRIME keyboard event registration
        VRIME_Manager.Ins.onCallIME.AddListener(imeShowHandle);
    }

    void OnDisable()
    {
        Debug.Log("FxRController.OnDisable()");

        // Clear the references to the plugin in any other objects in the scene that have it.
        FxRWindow[] fxrwindows = FindObjectsOfType<FxRWindow>();
        foreach (FxRWindow w in fxrwindows)
        {
            w.fxr_plugin = null;
        }

        fxr_plugin.fxrSetResourcesPath(null);

        // Since we might be going away, tell users of our Log function
        // to stop calling it.
        switch (Application.platform)
        {
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.OSXPlayer:
                goto case RuntimePlatform.WindowsPlayer;
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer:
            //case RuntimePlatform.LinuxEditor:
            case RuntimePlatform.LinuxPlayer:
                fxr_plugin.fxrRegisterLogCallback(null);
                break;
            case RuntimePlatform.Android:
                break;
            case RuntimePlatform.IPhonePlayer:
                break;
            case RuntimePlatform.WSAPlayerX86:
            case RuntimePlatform.WSAPlayerX64:
            case RuntimePlatform.WSAPlayerARM:
                fxr_plugin.fxrRegisterLogCallback(null);
                break;
            default:
                break;
        }
        fxr_plugin = null;

        // VRIME keyboard event registration
        VRIME_Manager.Ins.onCallIME.RemoveListener(imeShowHandle);
    }

    void Start()
    {
        Debug.Log("FxRController.Start()");

        Debug.Log("Fx version " + fxr_plugin.fxrGetFxVersion());

        fxr_plugin.fxrStartFx(OnFxWindowCreated);

        IntPtr openVRSession = UnityEngine.XR.XRDevice.GetNativePtr();
        if (openVRSession != IntPtr.Zero) {
            fxr_plugin.fxrSetOpenVRSessionPtr(openVRSession);
        }
    }

    public void ToggleKeyboard()
    {
        if (!VRIME_Manager.Ins.ShowState)
        {
            VRIME_Manager.Ins.ShowIME("");
        }
        else
        {
            VRIME_Manager.Ins.HideIME();
        }
    }

    private void imeShowHandle(bool iShow)
    {
        foreach (var laserPointer in LaserPointers)
        {
            laserPointer.enabled = !iShow;    
        }

        if (iShow) {
            SteamVR_Actions._default.GrabGrip.AddOnChangeListener(VRIME_Manager.Ins.MoveKeyboardHandle, SteamVR_Input_Sources.LeftHand);
            SteamVR_Actions._default.GrabGrip.AddOnChangeListener(VRIME_Manager.Ins.MoveKeyboardHandle, SteamVR_Input_Sources.RightHand);

            //SteamVR_Actions._default.TouchPress.AddOnChangeListener(VRIME_Manager.Ins.MoveCursorHandle, SteamVR_Input_Sources.LeftHand);
            //SteamVR_Actions._default.TouchPress.AddOnChangeListener(VRIME_Manager.Ins.MoveCursorHandle, SteamVR_Input_Sources.RightHand);

            //SteamVR_Actions._default.TouchPos.AddOnAxisListener(VRIME_Manager.Ins.MoveCursorPositionHandle, SteamVR_Input_Sources.LeftHand);
            //SteamVR_Actions._default.TouchPos.AddOnAxisListener(VRIME_Manager.Ins.MoveCursorPositionHandle, SteamVR_Input_Sources.RightHand);

        } else {
            SteamVR_Actions._default.GrabGrip.RemoveOnChangeListener(VRIME_Manager.Ins.MoveKeyboardHandle, SteamVR_Input_Sources.LeftHand);
            SteamVR_Actions._default.GrabGrip.RemoveOnChangeListener(VRIME_Manager.Ins.MoveKeyboardHandle, SteamVR_Input_Sources.RightHand);

            //SteamVR_Actions._default.TouchPress.RemoveOnChangeListener(VRIME_Manager.Ins.MoveCursorHandle, SteamVR_Input_Sources.LeftHand);
            //SteamVR_Actions._default.TouchPress.RemoveOnChangeListener(VRIME_Manager.Ins.MoveCursorHandle, SteamVR_Input_Sources.RightHand);

            //SteamVR_Actions._default.TouchPos.RemoveOnAxisListener(VRIME_Manager.Ins.MoveCursorPositionHandle, SteamVR_Input_Sources.LeftHand);
            //SteamVR_Actions._default.TouchPos.RemoveOnAxisListener(VRIME_Manager.Ins.MoveCursorPositionHandle, SteamVR_Input_Sources.RightHand);
        }
    }

    [AOT.MonoPInvokeCallback(typeof(FxRPluginWindowCreatedCallback))]
    void OnFxWindowCreated(int uid, int windowIndex, int widthPixels, int heightPixels, int formatNative)
    {
        Debug.Log("FxRController.OnFxWindowCreated(uid:" + uid + ", windowIndex:" + windowIndex + ", widthPixels:" + widthPixels + ", heightPixels:" + heightPixels + ", formatNative:" + formatNative + ")");

        FxRWindow window = FxRWindow.FindWindowWithUID(uid);
        if (window == null) {
            window = FxRWindow.CreateNewInParent(transform.parent.gameObject);
        }
        TextureFormat format;
        switch (formatNative)
        {
            case 1:
                format = TextureFormat.RGBA32;
                break;
            case 2:
                format = TextureFormat.BGRA32;
                break;
            case 3:
                format = TextureFormat.ARGB32;
                break;
            case 5:
                format = TextureFormat.RGB24;
                break;
            case 7:
                format = TextureFormat.RGBA4444;
                break;
            case 9:
                format = TextureFormat.RGB565;
                break;
            default:
                format = (TextureFormat)0;
                break;
        }
        window.WasCreated(windowIndex, widthPixels, heightPixels, format);
    }

    private void OnApplicationQuit()
    {
        Debug.Log("FxRController.OnApplicationQuit()");

        fxr_plugin.fxrStopFx();
    }

    // Update is called once per frame
    void Update()
    {
        //Debug.Log("FxRController.Update()");
    }

    public FXR_LOG_LEVEL LogLevel
    {
        get
        {
            return currentLogLevel;
        }

        set
        {
            currentLogLevel = value;
            fxr_plugin.fxrSetLogLevel((int)currentLogLevel);
        }
    }


}
