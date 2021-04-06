using UnityEditor;
using UnityEngine;

namespace Comms
{

    [CustomEditor(typeof(ReliableCommunication))]
    [CanEditMultipleObjects]
    public class ReliableCommunicationEditorUI : Editor
    {

        SerializedProperty isServer, serverport;

        private static GUIStyle ToggleButtonStyleNormal = null;
        private static GUIStyle ToggleButtonStyleToggled = null;


        private static string[] eventHandlerToolbar = new string[] { "Configuration", "Data Events", "Status Events" };
        private int openTab = 0;

        private static string[] ipStrategyToolbar = new string[] {"Manual", "Web", "UDP"};

        private bool expandWebExtras = false;

        private void OnEnable()
        {
            isServer = serializedObject.FindProperty("isServer");
            serverport = serializedObject.FindProperty("ListenPort");
        }


        public override void OnInspectorGUI()
        {
            ReliableCommunication c = (ReliableCommunication)target;
            if (ToggleButtonStyleNormal == null)
            {
                ToggleButtonStyleNormal = "Button";
                ToggleButtonStyleToggled = new GUIStyle(ToggleButtonStyleNormal);
                ToggleButtonStyleToggled.normal.background = ToggleButtonStyleToggled.active.background;
            }


            /* INFO BOX */
            if (c.isServer) {
                EditorGUILayout.HelpBox(string.Format("{0}\nTCP {1}{2}", 
                    c.Host.Name,
                    string.Format("Server on port {0}", c.ListenPort),
                    (c.dynamicMessageLength ? "" : ("\nStatic Messages with length " + c.fixedMessageLength))
                    ), MessageType.Info, true);
            }
            else {
                if (c.strategy == TargettingStrategy.Web) {
                    EditorGUILayout.HelpBox(string.Format("{0}\nTCP {1}{2}", 
                        c.Host.Name,
                        string.Format("Client of {0} room @ {1}", 
                            c.room, 
                            c.webserverSyncAddress),
                        (c.dynamicMessageLength ? "" : ("\nStatic Messages with length " + c.fixedMessageLength))
                        ), MessageType.Info, true);
                }
                // else if (c.strategy == TargettingStrategy.Udp) {

                // }
                else {
                    EditorGUILayout.HelpBox(string.Format("{0}\nTCP {1}{2}", 
                        c.Host.Name,
                        string.Format("Client of {0}:{1}", 
                            c.Host.Address, 
                            c.Host.Port, 
                            c.Host.Name),
                        (c.dynamicMessageLength ? "" : ("\nStatic Messages with length " + c.fixedMessageLength))
                        ), MessageType.Info, true);
                }
            }
            
            EditorGUILayout.LabelField("", GUILayout.Height(5));

            if (c.dropAccumulatedMessages)
            {
                EditorGUILayout.HelpBox("This socket will only call event handlers with the last message received", MessageType.Warning, true);
            }

            /* CONFIG/DATA/STATUS TABS */
            openTab = GUILayout.Toolbar(openTab, eventHandlerToolbar);
            switch (openTab)
            {
                case 1: //DataEvents
                    EditorGUILayout.LabelField("Should we drop acummulated packets?", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("dropAccumulatedMessages"));
                    EditorGUILayout.LabelField("", GUILayout.Height(6));

                    // JSON / string / byte messages
                    EditorGUILayout.LabelField("What kinds of messages will be handled here?", EditorStyles.boldLabel);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Bytes", c.EventType == CommunicationMessageType.Byte ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
                    {
                        c.EventType = CommunicationMessageType.Byte;
                    }
                    if (GUILayout.Button("String", c.EventType == CommunicationMessageType.String ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
                    {
                        c.EventType = CommunicationMessageType.String;
                    }
                    if (GUILayout.Button("Json", c.EventType == CommunicationMessageType.Json ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
                    {
                        c.EventType = CommunicationMessageType.Json;
                    }
                    GUILayout.EndHorizontal();


                    // Message Handlers
                    string handlerType = c.EventType.ToString() + "MessageReceived";
                    SerializedProperty eventHandler = serializedObject.FindProperty(handlerType);
                    EditorGUILayout.PropertyField(eventHandler);
                    break;

                case 2: // Status events
                    EditorGUILayout.LabelField("When the socket connects (or a client connects to it):", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("OnConnect"));

                    EditorGUILayout.LabelField("When the socket disconnects (or a client disconnects from it):", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("OnDisconnect"));

                    //EditorGUILayout.LabelField("When the socket raises an error:", EditorStyles.boldLabel);
                    //EditorGUILayout.PropertyField(serializedObject.FindProperty("OnError"));
                    break;

                default: // Configuration
                    EditorGUILayout.LabelField("Socket name, type, and host/port", EditorStyles.boldLabel);
                    EditorGUI.indentLevel += 1;

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(string.Format("Socket type", GUILayout.Width(10)));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Server", isServer.boolValue ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
                    {
                        isServer.boolValue = true;

                    }
                    if (GUILayout.Button("Client", !isServer.boolValue ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
                    {
                        isServer.boolValue = false;
                    }
                    GUILayout.EndHorizontal();
                    c.Host.Name = EditorGUILayout.TextField("Endpoint Name", c.Host.Name);
                    
                    if (c.isServer)
                    {
                        // Get port to host on
                        c.ListenPort = EditorGUILayout.IntField("Server Port:", c.ListenPort);
                    }

                    /* Connection Strategy (Manual/Web/Udp) */
                    EditorGUI.indentLevel++;
                    c.strategy = (TargettingStrategy) GUILayout.Toolbar((int)c.strategy, ipStrategyToolbar);
                    switch (c.strategy) {
                        case TargettingStrategy.Manual:
                            c.strategy = TargettingStrategy.Manual;
                            this.drawManualIpStrategy(c);
                            break;
                        case TargettingStrategy.Web:
                            c.strategy = TargettingStrategy.Web;
                            this.drawWebIpStrategy(c);
                            break;
                        case TargettingStrategy.UDP:
                            c.strategy = TargettingStrategy.UDP;
                            this.drawUdpIpStrategy(c);
                            break;
                        default:
                            EditorGUILayout.LabelField("Unknown type" + c.strategy);
                            break;
                    }
                    EditorGUI.indentLevel--;

                    this.drawMessageHeaders(c);
                    break;

            }

            serializedObject.ApplyModifiedProperties();

        }

        public void drawWebIpStrategy(ReliableCommunication c) {
            EditorGUILayout.LabelField("If your devices are on different networks or have variable IP addresses, you may wish to use this method to find the device before connecting");
            
            // Choose a Room
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Room");
            c.room = EditorGUILayout.TextField(c.room);
            if (GUILayout.Button("Generate")) {
                c.room = System.Guid.NewGuid().ToString();
            }
            EditorGUILayout.EndHorizontal();

            // Set the passkey
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Passkey");
            c.key = EditorGUILayout.TextField(c.key);
            if (GUILayout.Button("Generate")) {
                c.key = System.Guid.NewGuid().ToString();
            }
            EditorGUILayout.EndHorizontal();

            // More
            this.expandWebExtras = EditorGUILayout.BeginFoldoutHeaderGroup(this.expandWebExtras, "more");
            if (this.expandWebExtras) {
                // Set the webserver
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("web portal");
                c.webserverSyncAddress = EditorGUILayout.TextField(c.webserverSyncAddress);
                EditorGUILayout.EndHorizontal();

                // Set the ID
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("ID");
                c.id = EditorGUILayout.TextField(c.id);
                if (GUILayout.Button("Generate")) {
                    c.id = System.Guid.NewGuid().ToString();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        public void drawUdpIpStrategy(ReliableCommunication c) {
            EditorGUILayout.LabelField("Let's setup with Udp");
        }

        public void drawManualIpStrategy(ReliableCommunication c) {
            EditorGUILayout.LabelField("Manually set the IP address and port of the device you want to connect to.");
            // Server / Client Address/Port
            if (!c.isServer)
            {
                // Get Server Address
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("host:", GUILayout.Width(40));
                c.Host.Address = EditorGUILayout.TextField(GUIContent.none, c.Host.Address, GUILayout.MinWidth(40));
                EditorGUILayout.LabelField("port:", GUILayout.Width(40));
                c.Host.Port = EditorGUILayout.IntField(GUIContent.none, c.Host.Port, GUILayout.MinWidth(40));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("", GUILayout.Height(5));
            EditorGUI.indentLevel -= 1;
        }


        /// <summary>
        /// Draw the GUI for:
        /// Whether or not to have dynamic message sizes or a fixed message size
        /// </summary>
        /// <param name="c"></param>
        public void drawMessageHeaders(ReliableCommunication c) {
            // Message Headers
            EditorGUILayout.LabelField(string.Format("Message Length ({0})", c.dynamicMessageLength ? "dynamic" : "fixed"), EditorStyles.boldLabel);
            EditorGUI.indentLevel += 1;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Toggle for dynamic messages", c.dynamicMessageLength ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
            {
                c.dynamicMessageLength = !c.dynamicMessageLength;
            }
            // if (GUILayout.Button("Time Sent", c.TimeHeader ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
            // {
            //     c.TimeHeader = !c.TimeHeader;
            // }
            // if (GUILayout.Button("Calculate Bandwidth", c.MonitorBandwidth ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
            // {
            //     c.MonitorBandwidth = !c.MonitorBandwidth;
            //}
            EditorGUILayout.EndHorizontal();
            if (!c.dynamicMessageLength)
            {
                c.fixedMessageLength = EditorGUILayout.IntField("Fixed Message Size (bytes)", c.fixedMessageLength);
            }
            EditorGUI.indentLevel -= 1;
        }
    }
}