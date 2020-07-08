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

            EditorGUILayout.HelpBox(string.Format("{0}\nTCP {1}{2}", c.Host.Name,
                    c.isServer ?
                        string.Format("Server on port {0}", c.ListenPort) :
                        string.Format("Client of {0}:{1}", c.Host.Address, c.Host.Port, c.Host.Name),
                    (c.dynamicMessageLength ? "" : ("\nStatic Messages with length " + c.fixedMessageLength))
                    ), MessageType.Info, true);
            EditorGUILayout.LabelField("", GUILayout.Height(5));

            if (c.dropAccumulatedMessages)
            {
                EditorGUILayout.HelpBox("This socket will only call event handlers with the last message received", MessageType.Warning, true);
            }

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


                    // Server / Client Address/Port
                    if (c.isServer)
                    {
                        // Get port to host on
                        c.ListenPort = EditorGUILayout.IntField("Server Port:", c.ListenPort);
                    }
                    else
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

                    break;

            }

            serializedObject.ApplyModifiedProperties();

        }
    }
}