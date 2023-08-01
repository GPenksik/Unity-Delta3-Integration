using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using static DLLImportTest;

public class DLLImportTest : MonoBehaviour
{

    public enum DHD_STATUS_ENUM {
        DHD_STATUS_POWER,
        DHD_STATUS_CONNECTED,
        DHD_STATUS_STARTED,
        DHD_STATUS_RESET,
        DHD_STATUS_IDLE,
        DHD_STATUS_FORCE,
        DHD_STATUS_BRAKE,
        DHD_STATUS_TORQUE,
        DHD_STATUS_WRIST_DETECTED,
        DHD_STATUS_ERROR,
        DHD_STATUS_GRAVITY,
        DHD_STATUS_TIMEGUARD,
        DHD_STATUS_WRIST_INIT,
        DHD_STATUS_REDUNDANCY,
        DHD_STATUS_FORCE_OFF_CAUSE,
        DHD_STATUS_LOCKS,
        DHD_STATUS_AXIS_CHECKED
    }

    private bool forcesOn = false;

    public double forceTest = 4.0;
    public enum deviceStatus { DELTA_OPEN, DELTA_CLOSED };

    public Transform targetTransform;

    public deviceStatus DeviceStatus = deviceStatus.DELTA_CLOSED;

    public GameObject EndEffector;

    public Vector3 DhdPosition = Vector3.zero;

    IntPtr defaultId = new IntPtr(1);

    const int DHD_MAX_STATUS = 17;

    int[] DHD_STATUS_RESULT = new int[DHD_MAX_STATUS];

    public List<DHD_STATUS_ENUM> dhdStatus = new List<DHD_STATUS_ENUM>();

    [DllImport("dhd64.dll")]
    extern static int dhdOpen();

    [DllImport("dhd64.dll")]
    extern static int dhdStop(IntPtr id);

    [DllImport("dhd64.dll")]
    extern static int dhdClose(IntPtr id);

    [DllImport("dhd64.dll")]
    extern static int dhdGetStatus(int[] dhdStatus, IntPtr id);

    [DllImport("dhd64.dll")]
    extern static int dhdSetBrakes(int val, IntPtr id);

    [DllImport("dhd64.dll")]
    extern static IntPtr dhdErrorGetLastStr();

    [DllImport("dhd64.dll")]
    extern static void dhdSleep(double sec);

    [DllImport("dhd64.dll")]
    extern static int dhdGetPosition(ref double px, ref double py, ref double pz, IntPtr id);

    [DllImport("dhd64.dll")]
    extern static int dhdEnableForce(UIntPtr val, IntPtr id);

    [DllImport("dhd64.dll")]
    extern static int dhdSetStandardGravity(double g, IntPtr id);

    [DllImport("dhd64.dll")]
    extern static int dhdSetForce(double fx, double fy, double fz, IntPtr id);


    // Start is called before the first frame update
    void Start()
    {
        DhdOpen();
        if (dhdEnableForce(new UIntPtr(1), defaultId) >= 0)
        {
            Debug.Log("Forces set to on");
            forcesOn = true;
        } else
        {
            Debug.LogError("ERROR SETTING FORCES TO ON");
        }

        UpdateDHDStatus();
    }

    // Update is called once per frame

    private void FixedUpdate()
    {
        if (DeviceStatus == deviceStatus.DELTA_OPEN)
        {
            if (GetDHDPosition() >= 0) {
                EndEffector.transform.position = DhdPosition;
            }

            if (forcesOn)
            {
                float distanceToTarget = Vector3.Distance(DhdPosition, targetTransform.position);
                Vector3 directionToTarget = DhdPosition - targetTransform.position;
                directionToTarget.Normalize();
                Vector3 forceVector =  directionToTarget * (float)forceTest;
                ApplyForceTest(true, forceVector);
            }

        }


    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            DhdClose();
            UpdateDHDStatus();
        }
        if (Input.GetKeyDown(KeyCode.O)) {
            DhdOpen();
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            GetDHDPosition();
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            SetDHDBrake(false);
        }
        if (Input.GetKeyDown(KeyCode.V))
        {
            SetDHDBrake(true);
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            UpdateDHDStatus();
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            GetLastDHDError();
        }
        if (Input.GetKeyDown(KeyCode.G))
        {
            SetDHDGravity(0.0);
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            ApplyForceTest(true, new Vector3((float)forceTest,(float)forceTest,(float)forceTest));
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            ApplyForceTest(false, new Vector3((float)0.0f, (float)0.0f, (float)0.0f));
        }
    }

    private void OnDestroy()
    {
        DhdClose();
    }

    public int ApplyForceTest(bool on, Vector3 forceVector)
    {
        //double forceTest = 0.04;
        if (on)
        {
            dhdSetForce(forceVector.x, forceVector.y, forceVector.z, defaultId);
            return 1;
        } else
        {
            dhdSetForce(0.0,0.0,0.0, defaultId);
            return 1;
        }
    }
    public int DhdOpen()
    {
        // open the first available device
        if (dhdOpen() < 0)
        {
            DeviceStatus = deviceStatus.DELTA_CLOSED;
            IntPtr intPtr = dhdErrorGetLastStr();
            string myErrorString = Marshal.PtrToStringAnsi(intPtr);
            Debug.LogError(String.Format("error: cannot open device {0}\n", myErrorString));
            dhdSleep(2.0);
            return -1;
        }
        else
        {
            Debug.Log(String.Format("Device Succesfully Opened"));
            DeviceStatus = deviceStatus.DELTA_OPEN;
            UpdateDHDStatus();
            return 0;
        }
    }

    public int DhdClose()
    {
        if (dhdClose(defaultId) < 0)
        {
            IntPtr intPtr = dhdErrorGetLastStr();
            string myErrorString = Marshal.PtrToStringAnsi(intPtr);
            Debug.LogError(String.Format("error: Failed to stop device {0}\n", myErrorString));
            return -1;
        }
        else
        {
            DeviceStatus = deviceStatus.DELTA_CLOSED;
            Debug.Log(String.Format("Device Closed!"));
            return 0;
        }
    }
    private int UpdateDHDStatus()
    {

        if (dhdGetStatus(DHD_STATUS_RESULT, defaultId) < 0)
        {
            dhdStatus.Clear();
            return -1;
        }
        else
        {
            //Debug.Log(String.Format("Succesfully Got Status"));
            dhdStatus.Clear();
            for (int i = 0; i < DHD_MAX_STATUS; i++)
            {
                int currentResult = DHD_STATUS_RESULT[i];
                if (currentResult > 0)
                {
                    dhdStatus.Add((DHD_STATUS_ENUM)i);
                }
            }
            return 0;
        }
    }

    public int GetDHDPosition()
    {
        double px = 0;
        double py = 0;
        double pz = 0;
        if (dhdGetPosition(ref px, ref py, ref pz, defaultId) < 0) {
            Debug.LogError("ERROR GETTING POSITION");
            return -1;
        } else
        {
            //Debug.Log(String.Format("{0:0.000},{1:0.000},{2:0.000}", px, py, pz));
            DhdPosition = new Vector3((float)px, (float)pz, (float)py);
            return 0;
        }
    }

    public int SetDHDBrake(bool brakeOn)
    {
        int success = -1;

        if (brakeOn)
        {
            dhdEnableForce(new UIntPtr(1), defaultId);
            if (dhdSetBrakes(1, defaultId) < 0)
            {
                Debug.LogError("ERROR TURNING ON BRAKE");
                success = -1;
            }
            else
            {
                success = 0;
            }
        }
        else
        {
            dhdEnableForce(new UIntPtr(0), defaultId);
            if (dhdSetBrakes(0, defaultId) < 0)
            {
                Debug.LogError("ERROR TURNING OFF BRAKE");
                success = -1;
            }
            else
            {
                success = 0;
            }
        }
        UpdateDHDStatus();
        return success;
    }

    public void GetLastDHDError()
    {
        IntPtr intPtr = dhdErrorGetLastStr();
        string myErrorString = Marshal.PtrToStringAnsi(intPtr);
        Debug.LogError(String.Format("Last Error: {0}\n", myErrorString));
    }

    public void SetDHDGravity(double g)
    {
        dhdSetStandardGravity(g, defaultId);
    }
}
