#if (UNITY_EDITOR) 
using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace UcbEditorWindow
{
    public class UcbQrPopup : EditorWindow
    {
        public string QrString;

        public string msgString;
        
        public UcbQrPopup()
        {
        }

        private void OnEnable()
        {
            minSize = new Vector2(300, 350);
            maxSize = new Vector2(300, 350);
        }

        public static void Open(string qrString, string title = null, string addMsg = null)
        {
            UcbQrPopup window = ScriptableObject.CreateInstance(typeof(UcbQrPopup)) as UcbQrPopup;
            if (title != null)
            {
                window.titleContent = new GUIContent(title);
            }
            window.QrString = qrString;
            window.msgString = addMsg;
            window.ShowUtility();
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(QrString))
            {
                DrawQrCode(QrString);
                GUI.color = Color.white;
                
                GUIStyle gUIStyle = new GUIStyle() { alignment = TextAnchor.MiddleCenter, richText = true };
                EditorGUI.LabelField(new Rect(0, 270, position.width, 20), "<color=#aaaaaa>" + msgString + "</color>", gUIStyle);
            }
        }

        private void DrawQrCode(string content)
        {
            float margin = 50;
            float qrSize = Math.Min(position.width - margin * 2, 200);
            margin = Math.Max(margin, (position.width - qrSize) / 2);

            GUI.DrawTexture(new Rect(margin, margin, qrSize, qrSize), UcbUtils.QRHelper.generateQR(content), ScaleMode.ScaleToFit);
        }
    }
}
#endif