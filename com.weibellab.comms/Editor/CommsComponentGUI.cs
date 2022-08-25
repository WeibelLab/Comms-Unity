using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System;

namespace Comms
{
    [CustomEditor(typeof(CommsComponent))]
    public class CommsComponentGUI : Editor
    {
        private static int DEFAULT_SPACING = 25;
        private CommsComponent cc;
        private SerializedProperty config;
        private bool advanced = false;
        private SerializedProperty commsTypeIndex;

        // Events
        private SerializedProperty OnByteMessageReceived;
        private SerializedProperty OnStringMessageReceived;
        private SerializedProperty OnJsonMessageReceived;
        private SerializedProperty OnClientConnected;
        private SerializedProperty OnClientDisconnected;
        private SerializedProperty OnClientError;






        private void OnEnable()
        {
            cc = (CommsComponent)target;
            this.config = serializedObject.FindProperty("config");
            this.commsTypeIndex = serializedObject.FindProperty("CommsTypeIndex");

            // Events
            this.OnByteMessageReceived = serializedObject.FindProperty("OnByteMessageReceived");
            this.OnStringMessageReceived = serializedObject.FindProperty("OnStringMessageReceived");
            this.OnJsonMessageReceived = serializedObject.FindProperty("OnJsonMessageReceived");
            this.OnClientConnected = serializedObject.FindProperty("OnClientConnected");
            this.OnClientDisconnected = serializedObject.FindProperty("OnClientDisconnected");
            this.OnClientError = serializedObject.FindProperty("OnClientError");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ShowCommsType();
            ShowAbout();
            ShowConfig();
            ShowEvents();
            ShowHeader();

            if (GUI.changed)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }


        private void ShowCommsType()
        {
            // Create options
            string[] options = new string[CommsComponent.CommsNames.Length + 1];
            options[0] = "Select a type";
            CommsComponent.CommsNames.CopyTo(options, 1);

            // Get user selection
            EditorGUILayout.BeginHorizontal();
            float labelWidthBefore = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 30;
            commsTypeIndex.intValue = EditorGUILayout.Popup("Type", commsTypeIndex.intValue + 1, options) - 1;
            EditorGUIUtility.labelWidth = 70;
            this.advanced = EditorGUILayout.Toggle("  Advanced", this.advanced);
            EditorGUIUtility.labelWidth = labelWidthBefore;
            EditorGUILayout.EndHorizontal();
        }


        private void ShowAbout()
        {
            if (commsTypeIndex.intValue < 0) return;

            if (CommsComponent.CommsTypes[commsTypeIndex.intValue].ToString().ToLower().Contains("server"))
            {
                EditorGUILayout.LabelField($"{cc.config.Endpoint.Name} hosting on {cc.config.Endpoint.Address}:{cc.config.Endpoint.Port}", EditorStyles.boldLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"{cc.config.Endpoint.Name} connecting to {cc.config.Endpoint.Address}:{cc.config.Endpoint.Port}", EditorStyles.boldLabel);
            }
        }


        private void ShowConfig()
        {
            if (commsTypeIndex.intValue < 0) return;
            EditorGUILayout.Space(DEFAULT_SPACING);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            // Name
            cc.config.Endpoint.Name = EditorGUILayout.TextField("Name of this connection", cc.config.Endpoint.Name);
            // IP/Port
            EditorGUILayout.BeginHorizontal();
            float labelWidthBefore = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 60;
            cc.config.Endpoint.Address = EditorGUILayout.TextField("IP Address", cc.config.Endpoint.Address);
            EditorGUIUtility.labelWidth = 30;
            cc.config.Endpoint.Port = EditorGUILayout.IntField("Port", cc.config.Endpoint.Port);
            EditorGUIUtility.labelWidth = labelWidthBefore;
            EditorGUILayout.EndHorizontal();
            // Dropping/Spreading messages
            if (advanced)
            {
                cc.config.DropAccumulatedMessages = EditorGUILayout.Toggle("Drop Accumulated Messages", cc.config.DropAccumulatedMessages);
                cc.config.MaxMessagesPerFrame = EditorGUILayout.IntField("Limit messages per frame to (-1 to not)", cc.config.MaxMessagesPerFrame);
                cc.Quiet = EditorGUILayout.Toggle("Quiet", cc.Quiet);
            }
        }


        // Events
        private void ShowEvents()
        {
            if (commsTypeIndex.intValue < 0) return;
            // Message events (only for comms that can listen)
            if (CommsComponent.CommsAbilities[commsTypeIndex.intValue] == 1 || CommsComponent.CommsAbilities[commsTypeIndex.intValue] == 3)
            {
                EditorGUILayout.Space(DEFAULT_SPACING);
                EditorGUILayout.LabelField("Message Handling", EditorStyles.boldLabel);

                // Message Type
                cc.config.MessageType = (CommunicationMessageType)EditorGUILayout.EnumPopup("Type of messages", cc.config.MessageType);

                if (cc.config.MessageType == CommunicationMessageType.Byte)
                {
                    EditorGUILayout.PropertyField(this.OnByteMessageReceived);
                }
                else if (cc.config.MessageType == CommunicationMessageType.String)
                {
                    EditorGUILayout.PropertyField(this.OnStringMessageReceived);
                }
                else
                {
                    EditorGUILayout.PropertyField(this.OnJsonMessageReceived);
                }
            }

            // Connect/Disconnect/Error events
            if (advanced)
            {
                EditorGUILayout.Space(DEFAULT_SPACING);
                EditorGUILayout.LabelField("Status Events", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(this.OnClientConnected);
                EditorGUILayout.PropertyField(this.OnClientDisconnected);
                EditorGUILayout.PropertyField(this.OnClientError);
            }
        }


        // Headers
        private void ShowHeader()
        {
            if (commsTypeIndex.intValue < 0) return;
            EditorGUILayout.Space(DEFAULT_SPACING);
            EditorGUILayout.LabelField("Message Headers", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Todo..."); // TODO: implement

        }
    }
}
