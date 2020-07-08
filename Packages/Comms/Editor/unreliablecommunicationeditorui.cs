using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Comms
{

    [CustomEditor(typeof(UnreliableCommunication))]
    [CanEditMultipleObjects]
    public class UnreliableCommunicationEditorUI : Editor
    {

        private static GUIStyle ToggleButtonStyleNormal = null;
        private static GUIStyle ToggleButtonStyleToggled = null;
        private bool lockEditor = false;

        private List<string> names = new List<string>();
        private List<string> addrs = new List<string>();
        private List<int> ports = new List<int>();

        public override void OnInspectorGUI()
        {
            UnreliableCommunication c = (UnreliableCommunication)target;

            if (!lockEditor)
            {
                if (ToggleButtonStyleNormal == null)
                {
                    ToggleButtonStyleNormal = "Button";
                    ToggleButtonStyleToggled = new GUIStyle(ToggleButtonStyleNormal);
                    ToggleButtonStyleToggled.normal.background = ToggleButtonStyleToggled.active.background;
                }

                // Server / Client
                //EditorGUILayout.LabelField(string.Format("ReliableCommunication: {0} at {1}:{2}", c.isServer ? "server" : "client",
                //    c.isServer? "0.0.0.0" : c.Host.Address, c.isServer ? c.ListenPort : c.Host.Port), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Socket name and port", EditorStyles.boldLabel);
                EditorGUI.indentLevel += 1;

                // GUILayout.BeginHorizontal();
                //EditorGUILayout.LabelField(string.Format("Socket type", GUILayout.Width(10)));
                //GUILayout.FlexibleSpace();
                //if (GUILayout.Button("Unicast", c.isServer ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
                //{
                //    c.isServer = true;
                //}
                //if (GUILayout.Button("Broadcast", !c.isServer ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
                //{
                //    c.isServer = false;
                //}
                //GUILayout.EndHorizontal();


                // Server / Client Address/Port
                //if (c.isServer)
                //{
                // Get port to host on
                //    c.ListenPort = EditorGUILayout.IntField("Server Port:", c.ListenPort);
                //}
                //else
                //{
                // Get Server Address


                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("name:", GUILayout.Width(50));
                c.Name = EditorGUILayout.TextField(GUIContent.none, c.Name, GUILayout.MinWidth(40));
                EditorGUILayout.LabelField("port:", GUILayout.Width(40));
                c.port = EditorGUILayout.IntField(GUIContent.none, c.port, GUILayout.MinWidth(30));
                EditorGUILayout.EndHorizontal();
                //}
                EditorGUILayout.LabelField("", GUILayout.Height(5));
                EditorGUI.indentLevel -= 1;


                // Message Headers
                //EditorGUILayout.LabelField(string.Format("Message Length ({0})", c.dynamicMessageLength ? "dynamic" : "fixed"), EditorStyles.boldLabel);
                //EditorGUI.indentLevel += 1;
                //EditorGUILayout.BeginHorizontal();
                //if (GUILayout.Button("Toggle for dynamic messages", c.dynamicMessageLength ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
                //{
                //    c.dynamicMessageLength = !c.dynamicMessageLength;
                //}
                // if (GUILayout.Button("Time Sent", c.TimeHeader ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
                // {
                //     c.TimeHeader = !c.TimeHeader;
                // }
                // if (GUILayout.Button("Calculate Bandwidth", c.MonitorBandwidth ? ToggleButtonStyleToggled : ToggleButtonStyleNormal))
                // {
                //     c.MonitorBandwidth = !c.MonitorBandwidth;
                //}
                //EditorGUILayout.EndHorizontal();
                //if (!c.dynamicMessageLength)
                //{
                //    c.fixedMessageLength = EditorGUILayout.IntField("Fixed Message Size (bytes)", c.fixedMessageLength);
                //}
                //EditorGUI.indentLevel -= 1;

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
            }
            else
            {
                GUILayout.Label(string.Format("UDP Socket \"{0}\" listening on port {1}", c.Name, c.port));
            }

            // Target Devices
            this.PopulateLocalTargetVars(c);
            EditorGUILayout.LabelField("Broadcast Targets");
            EditorGUI.indentLevel += 1;
            for (int i = 0; i < c.Targets.Count; i++)
            {
                // Name, Remove
                EditorGUILayout.BeginHorizontal();
                this.names[i] = EditorGUILayout.TextField("Name", this.names[i]);
                if (GUILayout.Button("-"))
                {
                    this.addrs.RemoveAt(i);
                    this.ports.RemoveAt(i);
                    this.names.RemoveAt(i);
                    this.SetTargets(c);
                    this.PopulateLocalTargetVars(c);
                    return;
                }
                EditorGUILayout.EndHorizontal();
                // IP, Port
                EditorGUILayout.BeginHorizontal();
                this.addrs[i] = EditorGUILayout.TextField("Address", this.addrs[i]);
                this.ports[i] = EditorGUILayout.IntField("Port", this.ports[i]);
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+"))
            {
                c.Targets.Add(new CommunicationEndpoint("127.0.0.1", 12345));
                this.PopulateLocalTargetVars(c);
            }
            EditorGUI.indentLevel -= 1;
            this.SetTargets(c);

            // Message Handlers
            string handlerType = c.EventType.ToString() + "MessageReceived";
            SerializedProperty eventHandler = serializedObject.FindProperty(handlerType);
            EditorGUILayout.PropertyField(eventHandler);
            serializedObject.ApplyModifiedProperties();

            // Lock
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            this.lockEditor = EditorGUILayout.Toggle("lock", this.lockEditor);
            EditorGUILayout.EndHorizontal();
        }

        private void PopulateLocalTargetVars(UnreliableCommunication c)
        {
            this.names = new List<string>();
            this.addrs = new List<string>();
            this.ports = new List<int>();

            for (int i = 0; i < c.Targets.Count; i++)
            {
                this.names.Add(c.Targets[i].Name);
                this.addrs.Add(c.Targets[i].Address);
                this.ports.Add(c.Targets[i].Port);
            }
        }

        private void SetTargets(UnreliableCommunication c)
        {
            c.Targets = new List<CommunicationEndpoint>();
            for (int i = 0; i < this.addrs.Count; i++)
            {
                c.Targets.Add(new CommunicationEndpoint(this.addrs[i], this.ports[i], this.names[i]));
            }
        }
    }
}