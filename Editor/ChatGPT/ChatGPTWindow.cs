using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChatGPT.Editor
{
    public class ChatGPTWindow : EditorWindow
    {
        Vector2 scrollPos = Vector2.zero;

        ChatGPT ai;
        private bool settingFoldout = false;
        string message;
        const string aiRoleName = "AI";
        private float chatBoxWidthRatio = 0.85f;
        private float iconSizeRatio = 0.6f;
        private float iconMaxSize = 100f;
        private float chatBoxPadding = 20;
        private float chatBoxEdgePadding = 10;

        GUIStyle myChatStyle;
        GUIStyle aiChatStyle;

        GUIStyle aiIconStyle;
        GUIStyle myIconStyle;
        GUIStyle txtAreaStyle;

        GUIContent chatContent;

        bool isEditorInitialized = false;
        private float scrollViewHeight;

        string myApiKey;

        [MenuItem("ChatGPT/ChatGPT Window")]
        static void ChatGPTMenu()
        {
            var win = GetWindow<ChatGPTWindow>("ChatGPT");
            win.Show();
        }
        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            myApiKey = EditorPrefs.GetString("ChatGPT.Settings.APIKey", null);
            ai = new ChatGPT(myApiKey);
            ai.ChatGPTRandomness = EditorPrefs.GetFloat("ChatGPT.Settings.Temperature", 0f);
            chatContent = new GUIContent();
            ai.RestoreChatHistory();
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }
            try
            {
                InitGUIStyles();
                isEditorInitialized = true;
                EditorApplication.update -= OnEditorUpdate;
            }
            catch (Exception)
            {

            }
        }

        private void InitGUIStyles()
        {

            aiChatStyle = new GUIStyle(
#if UNITY_2021_1_OR_NEWER
                EditorStyles.selectionRect
#else
                EditorStyles.textArea
#endif
                );
            aiChatStyle.wordWrap = true;
            aiChatStyle.normal.textColor = Color.white;
            aiChatStyle.fontSize = 18;
            aiChatStyle.alignment = TextAnchor.MiddleLeft;

            myChatStyle = new GUIStyle(EditorStyles.helpBox);
            myChatStyle.wordWrap = true;
            myChatStyle.normal.textColor = Color.white;
            myChatStyle.fontSize = 18;
            myChatStyle.alignment = TextAnchor.MiddleLeft;


            txtAreaStyle = new GUIStyle(EditorStyles.textArea);
            txtAreaStyle.fontSize = 18;

            aiIconStyle = new GUIStyle();
            aiIconStyle.wordWrap = true;
            aiIconStyle.alignment = TextAnchor.MiddleCenter;
            aiIconStyle.fontSize = 18;
            aiIconStyle.fontStyle = FontStyle.Bold;
            aiIconStyle.normal.textColor = Color.black;
            aiIconStyle.normal.background = EditorGUIUtility.FindTexture("sv_icon_dot5_pix16_gizmo");

            myIconStyle = new GUIStyle(aiIconStyle);
            myIconStyle.normal.background = EditorGUIUtility.FindTexture("sv_icon_dot2_pix16_gizmo");
        }

        private void OnDisable()
        {
            ai.SaveChatHistory();
            SaveSettings();
        }


        private void OnGUI()
        {
            if (!isEditorInitialized) return;
            EditorGUILayout.BeginVertical();
            {
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                {
                    scrollViewHeight = 0;
                    for (int i = 0; i < ai.MessageHistory.Count; i++)
                    {
                        var msg = ai.MessageHistory[i];
                        var msgRect = EditorGUILayout.BeginVertical();
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                bool isMyMsg = ai.IsSelfMessage(msg);
                                var labelStyle = isMyMsg ? myChatStyle : aiChatStyle;
                                chatContent.text = msg.content;
                                float chatBoxWidth = this.position.width * chatBoxWidthRatio;
                                float iconSize = Mathf.Min(iconMaxSize, (this.position.width - chatBoxWidth) * iconSizeRatio);
                                float chatBoxHeight = Mathf.Max(iconSize, chatBoxEdgePadding + labelStyle.CalcHeight(chatContent, chatBoxWidth - chatBoxEdgePadding));
                                ChatGPTCodeBlock[] codeBlocks = null;
                                if (isMyMsg) { GUILayout.FlexibleSpace(); }
                                else
                                {
                                    codeBlocks = ai.GetCodeBlocksByIdx(i);
                                    if (codeBlocks != null)
                                    {
                                        chatBoxWidth -= 50;
                                    }
                                    EditorGUILayout.LabelField(aiRoleName, aiIconStyle, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
                                }

                                EditorGUILayout.SelectableLabel(msg.content, labelStyle, GUILayout.Width(chatBoxWidth), GUILayout.Height(chatBoxHeight));
                                if (!isMyMsg)
                                {
                                    if (codeBlocks != null)
                                    {
                                        for (int blockIdx = 0; blockIdx < codeBlocks.Length; blockIdx++)
                                        {
                                            var cBlock = codeBlocks[blockIdx];
                                            EditorGUILayout.BeginVertical("box");
                                            {
                                                if (GUILayout.Button($"Save {cBlock.FileExtension} File({blockIdx})"))
                                                {
                                                    var fileName = EditorUtility.SaveFilePanel("Save File", EditorPrefs.GetString("LAST_SELECT_PATH"), null, cBlock.FileExtension);
                                                    if (!string.IsNullOrWhiteSpace(fileName))
                                                    {
                                                        try
                                                        {
                                                            System.IO.File.WriteAllText(fileName, cBlock.Content, System.Text.Encoding.UTF8);
                                                            EditorPrefs.SetString("LAST_SELECT_PATH", Path.GetFullPath(fileName));
                                                            AssetDatabase.Refresh();
                                                        }
                                                        catch (Exception e)
                                                        {
                                                            Debug.LogError($"Save {cBlock.FileExtension} file failed:{e.Message}");
                                                        }
                                                    }
                                                }
                                                EditorGUILayout.EndVertical();
                                            }
                                        }
                                    }
                                    GUILayout.FlexibleSpace();
                                }
                                else
                                {
                                    EditorGUILayout.LabelField(msg.role, myIconStyle, GUILayout.Width(iconSize), GUILayout.Height(iconSize));
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            EditorGUILayout.EndVertical();
                        }
                        EditorGUILayout.Space(chatBoxPadding);
                        scrollViewHeight += msgRect.height;
                    }
                    EditorGUILayout.EndScrollView();
                }

                if (ai.IsRequesting)
                {
                    var barWidth = position.width * 0.8f;
                    var pBarRect = new Rect((position.width - barWidth) * 0.5f, (position.height - 30f) * 0.5f, barWidth, 30f);
                    EditorGUI.ProgressBar(pBarRect, ai.RequestProgress, $"Progress:{ai.RequestProgress:P2}");
                }
                GUILayout.FlexibleSpace();
                if (string.IsNullOrWhiteSpace(myApiKey)) EditorGUILayout.HelpBox("Please fill in the API Key in ChatGPT Settings first.", MessageType.Error);
                if (settingFoldout = EditorGUILayout.Foldout(settingFoldout, "ChatGPT Settings:"))
                {
                    EditorGUILayout.BeginVertical("box");
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
#if UNITY_2021_1_OR_NEWER
                            if (EditorGUILayout.LinkButton("Get API Key:", GUILayout.Width(170)))
#else
                            if (GUILayout.Button("Get API Key:", GUILayout.Width(170)))
#endif
                            {
                                Application.OpenURL("https://platform.openai.com/account/api-keys");
                            }
                            EditorGUI.BeginChangeCheck();
                            {
                                myApiKey = EditorGUILayout.PasswordField(myApiKey);
                                if (EditorGUI.EndChangeCheck())
                                {
                                    ai.SetAPIKey(myApiKey);
                                }
                            }
                            EditorGUILayout.EndHorizontal();
                        }

                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField("Randomness", GUILayout.Width(170));
                            ai.ChatGPTRandomness = EditorGUILayout.Slider(ai.ChatGPTRandomness, 0, 2);
                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField("WebRequest Timeout:", GUILayout.Width(170));
                            ai.RequestTimeout = EditorGUILayout.IntSlider(ai.RequestTimeout, 30, 120);
                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.EndVertical();

                    }

                }
                EditorGUILayout.BeginHorizontal();
                {
                    message = EditorGUILayout.TextArea(message, txtAreaStyle, GUILayout.MinHeight(80));

                    EditorGUI.BeginDisabledGroup(ai.IsRequesting);
                    {
                        if (GUILayout.Button("Send", GUILayout.MaxWidth(120), GUILayout.Height(80)))
                        {
                            if (!string.IsNullOrWhiteSpace(message))
                            {
                                ai.Send(message, OnChatGPTMessage);
                            }
                        }
                        if (GUILayout.Button("New Chat", GUILayout.MaxWidth(80), GUILayout.Height(80)))
                        {
                            ai.NewChat();
                        }
                        EditorGUI.EndDisabledGroup();
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void OnChatGPTMessage(bool arg1, string arg2)
        {
            scrollPos.y = scrollViewHeight;
            if (arg1)
            {
                message = string.Empty;
            }
            Repaint();
        }

        private void SaveSettings()
        {
            EditorPrefs.SetFloat("ChatGPT.Settings.Temperature", ai.ChatGPTRandomness);
            EditorPrefs.SetString("ChatGPT.Settings.APIKey", myApiKey);
        }
    }
}

