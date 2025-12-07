using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace JayT.UnityAvatarTools.Facial
{
    /// <summary>
    /// iFacialMocap UDP レシーバー
    /// ARKitの52個のBlendShapeパラメータを受信
    /// </summary>
    public class IFacialMocapReceiver : MonoBehaviour
    {
        [Header("Network Settings")]
        [SerializeField] private int receivePort = 49983;
        
        [Header("PC IP Address (iFacialMocapに入力)")]
        [SerializeField] private string localIPAddress = "";
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLog = false;
        
        // BlendShapeデータ
        private Dictionary<string, float> blendShapeValues = new Dictionary<string, float>();
        
        // Head Transform
        private Vector3 headPosition = Vector3.zero;
        private Quaternion headRotation = Quaternion.identity;
        
        // Threading
        private UdpClient udpClient;
        private Thread receiveThread;
        private bool isRunning = false;
        
        // Thread-safe data exchange
        private readonly object lockObject = new object();
        private bool hasNewData = false;

        void OnValidate()
        {
            // EditorでローカルIPアドレスを自動取得して表示
            UpdateLocalIPAddress();
        }

        void Start()
        {
            UpdateLocalIPAddress();
            InitializeReceiver();
        }

        void OnDestroy()
        {
            StopReceiver();
        }

        private void UpdateLocalIPAddress()
        {
            try
            {
                localIPAddress = FetchLocalIPAddress();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to get local IP address: {e.Message}");
                localIPAddress = "IP取得失敗";
            }
        }

        private string FetchLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            
            // IPv4アドレスを優先的に取得
            var ipAddresses = host.AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                .ToList();
            
            if (ipAddresses.Count == 0)
            {
                return "IPアドレスが見つかりません";
            }
            
            // ループバックアドレス以外を優先
            var nonLoopback = ipAddresses.FirstOrDefault(ip => !IPAddress.IsLoopback(ip));
            if (nonLoopback != null)
            {
                return nonLoopback.ToString();
            }
            
            return ipAddresses[0].ToString();
        }

        private void InitializeReceiver()
        {
            try
            {
                udpClient = new UdpClient(receivePort);
                isRunning = true;
                
                receiveThread = new Thread(ReceiveData);
                receiveThread.IsBackground = true;
                receiveThread.Start();
                
                Debug.Log($"iFacialMocap Receiver started on port {receivePort}");
                Debug.Log($"iFacialMocapアプリに入力するIPアドレス: {localIPAddress}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start receiver: {e.Message}");
            }
        }

        private void StopReceiver()
        {
            isRunning = false;
            
            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Abort();
            }
            
            if (udpClient != null)
            {
                udpClient.Close();
            }
            
            Debug.Log("iFacialMocap Receiver stopped");
        }

        private void ReceiveData()
        {
            while (isRunning)
            {
                try
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, receivePort);
                    byte[] data = udpClient.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);
                    
                    ParseMessage(message);
                }
                catch (Exception e)
                {
                    if (isRunning)
                    {
                        Debug.LogError($"Receive error: {e.Message}");
                    }
                }
            }
        }

        private void ParseMessage(string message)
        {
            string[] parts = message.Split('|');
            
            if (parts.Length < 2)
            {
                return;
            }

            string messageType = parts[0];

            lock (lockObject)
            {
                switch (messageType)
                {
                    case "iFacialMocap_head":
                        ParseHeadData(parts);
                        break;
                        
                    case "iFacialMocap_blendShapes":
                        ParseBlendShapeData(parts);
                        break;
                }
                
                hasNewData = true;
            }
        }

        private void ParseHeadData(string[] parts)
        {
            // Format: iFacialMocap_head|rx|ry|rz|x|y|z
            if (parts.Length < 7)
            {
                return;
            }

            try
            {
                float rx = float.Parse(parts[1]);
                float ry = float.Parse(parts[2]);
                float rz = float.Parse(parts[3]);
                float x = float.Parse(parts[4]);
                float y = float.Parse(parts[5]);
                float z = float.Parse(parts[6]);

                // Unityの座標系に変換（必要に応じて調整）
                headRotation = Quaternion.Euler(rx, ry, rz);
                headPosition = new Vector3(x, y, z);

                if (showDebugLog)
                {
                    Debug.Log($"Head - Pos: {headPosition}, Rot: {headRotation.eulerAngles}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to parse head data: {e.Message}");
            }
        }

        private void ParseBlendShapeData(string[] parts)
        {
            // Format: iFacialMocap_blendShapes|key1&value1|key2&value2|...
            for (int i = 1; i < parts.Length; i++)
            {
                string[] keyValue = parts[i].Split('&');
                if (keyValue.Length == 2)
                {
                    try
                    {
                        string key = keyValue[0];
                        float value = float.Parse(keyValue[1]);
                        
                        blendShapeValues[key] = value;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to parse blend shape: {e.Message}");
                    }
                }
            }

            if (showDebugLog)
            {
                Debug.Log($"Received {blendShapeValues.Count} blend shapes");
            }
        }

        void Update()
        {
            // メインスレッドで新しいデータがあるか確認
            lock (lockObject)
            {
                if (hasNewData)
                {
                    hasNewData = false;
                    // ここで受信したデータを使用可能
                }
            }
        }

        #region Public API

        /// <summary>
        /// 指定したBlendShapeの値を取得
        /// </summary>
        public float GetBlendShapeValue(string blendShapeName)
        {
            lock (lockObject)
            {
                if (blendShapeValues.ContainsKey(blendShapeName))
                {
                    return blendShapeValues[blendShapeName];
                }
            }
            return 0f;
        }

        /// <summary>
        /// すべてのBlendShape値を取得
        /// </summary>
        public Dictionary<string, float> GetAllBlendShapeValues()
        {
            lock (lockObject)
            {
                return new Dictionary<string, float>(blendShapeValues);
            }
        }

        /// <summary>
        /// 頭の位置を取得
        /// </summary>
        public Vector3 GetHeadPosition()
        {
            lock (lockObject)
            {
                return headPosition;
            }
        }

        /// <summary>
        /// 頭の回転を取得
        /// </summary>
        public Quaternion GetHeadRotation()
        {
            lock (lockObject)
            {
                return headRotation;
            }
        }

        /// <summary>
        /// データ受信中かどうか
        /// </summary>
        public bool IsReceiving()
        {
            return isRunning;
        }

        /// <summary>
        /// このPCのIPアドレスを取得
        /// </summary>
        public string GetLocalIPAddress()
        {
            return localIPAddress;
        }

        #endregion
    }
}