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

        private static string[] ipStrategyToolbar = new string[] {"Manual (default)", "Web", "UDP (unsupported)"};
        private bool expandWebExtras = false;
        private static string[] messageFormatToolbar = new string[] { "Dynamic (default)", "Fixed Size", "Custom" };
        private string customMessageHeader;


        private void OnEnable()
        {
            isServer = serializedObject.FindProperty("isServer");
            serverport = serializedObject.FindProperty("ListenPort");
            ReliableCommunication c = (ReliableCommunication)target;
            this.customMessageHeader = c.HeaderFormat;
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
                    (c.messageFormat == CommunicationHeaderType.Fixed ? ("\nStatic Messages with length " + c.fixedMessageLength): "")
                    ), MessageType.Info, true);
            }
            else {
                if (c.strategy == TargettingStrategy.Web) {
                    EditorGUILayout.HelpBox(string.Format("{0}\nTCP {1}{2}", 
                        c.Host.Name,
                        string.Format("Client of {0} room @ {1}", 
                            c.room, 
                            c.webserverSyncAddress),
                        (c.messageFormat == CommunicationHeaderType.Fixed ? ("\nStatic Messages with length " + c.fixedMessageLength): "")
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
                        (c.messageFormat == CommunicationHeaderType.Fixed ? ("\nStatic Messages with length " + c.fixedMessageLength): "")
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
                    if (GUILayout.Button("Custom", c.EventType == CommunicationMessageType.Custom ? ToggleButtonStyleToggled: ToggleButtonStyleNormal))
                    {
                        c.EventType = CommunicationMessageType.Custom;
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
                    EditorGUILayout.Space();


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
                    c.Host.Name = EditorGUILayout.TextField("Connection Name", c.Host.Name);
                    
                    if (c.isServer)
                    {
                        // Get port to host on
                        c.ListenPort = EditorGUILayout.IntField("Server Port:", c.ListenPort);
                    }

                    

                    this.drawMessageHeaders(c);
                    break;

            }

            serializedObject.ApplyModifiedProperties();

        }

        public void drawWebIpStrategy(ReliableCommunication c) {
            EditorGUILayout.LabelField("If your devices have variable IPs, \nyou may wish to use this method to find\nthe device before connecting.\nUse github.com/WeibelLab/Comms/tree/master/webserver\nfor the server",
            GUILayout.MinHeight(80),
            GUILayout.MinWidth(400));
            EditorGUILayout.Space();
            // Web Portal
            c.webserverSyncAddress = EditorGUILayout.TextField("Web Portal", c.webserverSyncAddress);

            // Choose a Room
            EditorGUILayout.BeginHorizontal();
            c.room = EditorGUILayout.TextField("Room Name", c.room);
            if (GUILayout.Button("Generate")) {
                c.room = System.Guid.NewGuid().ToString();
            }
            EditorGUILayout.EndHorizontal();

            // Set the passkey
            EditorGUILayout.BeginHorizontal();
            c.key = EditorGUILayout.TextField("Room Passkey", c.key);
            if (GUILayout.Button("Generate")) {
                c.key = System.Guid.NewGuid().ToString();
            }
            EditorGUILayout.EndHorizontal();

            // Set the ID
            EditorGUILayout.BeginHorizontal();
            c.id = EditorGUILayout.TextField("This Connection's Name", c.id);
            if (GUILayout.Button("Generate")) {
                c.id = System.Guid.NewGuid().ToString();
            }
            EditorGUILayout.EndHorizontal();
        }

        public void drawUdpIpStrategy(ReliableCommunication c) {
            EditorGUILayout.LabelField("This feature haven't been implemented\nit will let you do a local network broadcast\nto find connections", 
            GUILayout.MinHeight(60),
            GUILayout.MinWidth(400));
            EditorGUILayout.Space();
        }

        public void drawManualIpStrategy(ReliableCommunication c) {
            EditorGUILayout.LabelField("Manually set the IP address and port\nof the device you want to connect to.",
            GUILayout.MinHeight(40),
            GUILayout.MinWidth(400));
            EditorGUILayout.Space();
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

            EditorGUI.indentLevel++;
            c.messageFormat = (CommunicationHeaderType)GUILayout.Toolbar((int)c.messageFormat, messageFormatToolbar);
            switch (c.messageFormat)
            {
                case CommunicationHeaderType.Fixed:
                    c.fixedMessageLength = EditorGUILayout.IntField("Fixed Message Size (bytes)", c.fixedMessageLength);
                    break;
                case CommunicationHeaderType.Dynamic:
                    EditorGUILayout.LabelField("All messages are prepended with a 4 byte integer describing the message length in bytes");
                    break;
                case CommunicationHeaderType.Custom:
                    drawCustomHeader(c);
                    break;
                default:
                    EditorGUILayout.LabelField("Unknown type" + c.strategy);
                    break;
            }
            EditorGUI.indentLevel--;
        }

        public void drawCustomHeader(ReliableCommunication c)
        {
            EditorGUILayout.BeginHorizontal();
            this.customMessageHeader = EditorGUILayout.TextArea(this.customMessageHeader, GUILayout.Height(80));
            
            var json = JSONObject.Create(customMessageHeader);
            if (json && json.type != JSONObject.Type.NULL && !json.ToString().Equals("{}"))
            {
                c.HeaderFormat = json.ToString();
                this.customMessageHeader = c.HeaderFormat;
            }
            else
            {
                EditorGUILayout.LabelField("Invalid JSON");
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}