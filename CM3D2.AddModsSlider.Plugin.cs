using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;
using UnityInjector.Attributes;
using CM3D2.ExternalSaveData.Managed;

namespace CM3D2.AddModsSlider.Plugin
{
    [PluginFilter("CM3D2x64"),
    PluginFilter("CM3D2x86"),
    PluginFilter("CM3D2VRx64"),
    PluginName("CM3D2 AddModsSlider"),
    PluginVersion("0.1.0.7")]
    public class AddModsSlider : UnityInjector.PluginBase
    {
        public const string Version = "0.1.0.7";

        private int sceneLevel;
        private bool visible = false;
        private bool xmlLoad = false;
        private bool oneLoad = false;
        private ModsParam mp;
        private PixelValues pv;

        private Maid maid;
        private string freeComment;
        private Rect winRect;
        private Vector2 lastScreenSize;

        private float modsSliderWidth = 0.25f;
         private Vector2 scrollViewVector = Vector2.zero;

        private class ModsParam 
        {
            public readonly string DefMatchPattern = @"([-+]?[0-9]*\.?[0-9]+)";
            public readonly string XmlFileName = Directory.GetCurrentDirectory() + @"\UnityInjector\Config\ModsParam.xml";
            
            // WideSlider有効化が前提のmod
            public List<string> NeedsWideSlider = new List<string> { "FARMFIX", "EYE_RATIO", "EYE_ANG", "PELSCL", "SKTSCL", "THISCL", "THIPOS"
                                                                    ,"SPISCL", "S0ASCL", "S1_SCL", "S1ASCL" };
            // MaidVoicePitch.0.2.2 準拠
            // MaidVoicePitchでBool値でのON/OFFフラグが無いmod
            public List<string> CantDisable     = new List<string> { "EYETOCAM", "HEADTOCAM", "PITCH", "MABATAKI", "FACE_ANIME_SPEED"
                                                                    ,"EYEBALL", "EYE_RATIO", "EYE_ANG", "PELSCL", "THISCL", "THIPOS" } ;
            // MaidVoicePitchでname=keyのvalueをmodの引数として扱ってるmod
            public List<string> IsNotBoolKey    = new List<string> { "EYETOCAM", "HEADTOCAM", "PITCH", "MABATAKI", "FACE_ANIME_SPEED", "EYE_RATIO" } ;

            public string XmlFormat;
            public List<string> sKey = new List<string>();

            public Dictionary<string, string>   sDescription  = new Dictionary<string, string>();
            public Dictionary<string, bool>     bForced       = new Dictionary<string, bool>();
            public Dictionary<string, bool>     bEnabled      = new Dictionary<string, bool>();
            public Dictionary<string, string>   sType         = new Dictionary<string, string>();

            public Dictionary<string, string[]> sPropName     = new Dictionary<string, string[]>();
            public Dictionary<string, Dictionary<string, float>>  fValue        = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, float>>  fVmin         = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, float>>  fVmax         = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, float>>  fVdefault     = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, string>> sVType        = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Dictionary<string, string>> sLabel        = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Dictionary<string, string>> sMatchPattern = new Dictionary<string, Dictionary<string, string>>();

            public int KeyCount { get{return sKey.Count;} }
            public int ValCount(string key) { return sPropName[key].Length; }

        //--------

            public ModsParam()
            {
                Init();
            }

            public bool Init()
            {
                if(!loadModsParamXML()) 
                {
                    Debug.LogError("AddModsSlider : loadModsParamXML() failed.");
                    return false;
                }

                return true;
            }


            private bool loadModsParamXML()
            {
                if (!File.Exists(XmlFileName)) 
                {
                    Debug.LogError("AddModsSlider : \"" + XmlFileName + "\" does not exist.");
                    return false;
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(XmlFileName);

                 XmlNode mods = doc.DocumentElement;
                 XmlFormat = ((XmlElement)mods).GetAttribute("format");
                if (XmlFormat != "1.1")
                {
                    Debug.LogError("AddModsSlider : "+ AddModsSlider.Version +" requires fomart=\"1.1\" of ModsParam.xml.");
                    return false;
                }

                XmlNodeList modNodeS = mods.SelectNodes("/mods/mod");
                if (!(modNodeS.Count > 0)) 
                {
                    Debug.LogError("AddModsSlider :  \"" + XmlFileName + "\" has no <mod>elements.");
                    return false;
                }

                sKey.Clear();

                foreach(XmlNode modNode in modNodeS)
                {    
                    // mod属性
                    string key = ((XmlElement)modNode).GetAttribute("id");
                    if(key != "" && !sKey.Contains(key)) sKey.Add(key);
                    else continue;

                    bool f = false;
                    sDescription[key] = ((XmlElement)modNode).GetAttribute("description");
                    bEnabled[key] = (Boolean.TryParse(((XmlElement)modNode).GetAttribute("enabled"), out f)) ? f : true;
                    bForced[key] = (Boolean.TryParse(((XmlElement)modNode).GetAttribute("forced"), out f)) ? f : true;

                    sType[key] = ((XmlElement)modNode).GetAttribute("type");
                    if (sType[key] == "") sType[key] = "slider";
                    if (sType[key] == "toggle") continue;

                    XmlNodeList valueNodeS = ((XmlElement)modNode).GetElementsByTagName("value");
                    if (!(valueNodeS.Count > 0)) continue;
                    
                    sPropName[key]     = new string[valueNodeS.Count];
                    fValue[key]        = new Dictionary<string, float>();
                    fVmin[key]         = new Dictionary<string, float>();
                    fVmax[key]         = new Dictionary<string, float>();
                    fVdefault[key]     = new Dictionary<string, float>();
                    sVType[key]        = new Dictionary<string, string>();
                    sLabel[key]        = new Dictionary<string, string>();
                    sMatchPattern[key] = new Dictionary<string, string>();
                    
                    // value属性
                    int j = 0;
                    foreach (XmlNode valueNode in valueNodeS)
                    {
                        float x = 0f;
                        
                        string prop = ((XmlElement)valueNode).GetAttribute("prop_name");
                        if (prop != "" && Array.IndexOf(sPropName[key], prop) < 0 ) sPropName[key][j] = prop;
                        else
                        {    
                            sKey.Remove(key);
                            break;
                        }
                        
                        fVmin[key][prop] = Single.TryParse(((XmlElement)valueNode).GetAttribute("min"), out x) ? x : 0f;
                        fVmax[key][prop] = Single.TryParse(((XmlElement)valueNode).GetAttribute("max"), out x) ? x : 0f;
                        fVdefault[key][prop] = Single.TryParse(((XmlElement)valueNode).GetAttribute("default"), out x) ? x : 0f;

                        sVType[key][prop] = ((XmlElement)valueNode).GetAttribute("type");
                        switch (sVType[key][prop])
                        {
                            case "num":   break;
                            case "scale": break;
                            case "int" : break;
                            default : sVType[key][prop] = "num"; break;
                        }

                        sLabel[key][prop] = ((XmlElement)valueNode).GetAttribute("label");
                        sMatchPattern[key][prop] = ((XmlElement)valueNode).GetAttribute("match_pattern");

                        j++;
                    }
                    if (j == 0) sKey.Remove(key);
                }
                
                return true;
            }


        }

        private class PixelValues
        {
            public float BaseWidth = 1280f;
            public float PropRatio = 0.6f;
            public int Margin;

            private Dictionary<string, int> font = new Dictionary<string, int>();
            private Dictionary<string, int> line = new Dictionary<string, int>();
            private Dictionary<string, int> sys =  new Dictionary<string, int>();

            public PixelValues()
            {
                Margin = PropPx(10);

                font["C1"] = 11;
                font["C2"] = 12;
                font["H1"] = 14;
                font["H2"] = 16;
                font["H3"] = 20;

                line["C1"] = 14;
                line["C2"] = 18;
                line["H1"] = 22;
                line["H2"] = 24;
                line["H3"] = 30;

                sys["Menu.Height"] = 45;
                sys["OkButton.Height"] = 95;

                sys["HScrollBar.Width"] = 15;
            }
            
            public int Font(string key) { return PropPx(font[key]); }
            public int Line(string key) { return PropPx(line[key]); }
            public int Sys(string key)  { return PropPx(sys[key]); }

            public int Font_(string key) { return font[key]; }
            public int Line_(string key) { return line[key]; }
            public int Sys_(string key)  { return sys[key]; }

            public Rect PropScreen(float left, float top, float width, float height)
            {
                return new Rect((int)((Screen.width - Margin * 2) * left + Margin)
                               ,(int)((Screen.height - Margin * 2) * top + Margin)
                               ,(int)((Screen.width - Margin * 2) * width)
                               ,(int)((Screen.height - Margin * 2) * height));
            }

            public Rect PropScreenMH(float left, float top, float width, float height)
            {
                Rect r = PropScreen(left, top, width, height);
                r.y += Sys("Menu.Height");
                r.height -= (Sys("Menu.Height") + Sys("OkButton.Height"));
                
                return r;
            }

            public Rect PropScreenMH(float left, float top, float width, float height, Vector2 last)
            {
                Rect r = PropScreen((float)(left/(last.x - Margin * 2)), (float)(top/(last.y - Margin * 2)), width, height);
                r.height -= (Sys("Menu.Height") + Sys("OkButton.Height"));
                
                return r;
            }

            public Rect InsideRect(Rect rect) 
            {
                return new Rect(Margin, Margin, rect.width - Margin * 2, rect.height - Margin * 2);
            }

            public Rect InsideRect(Rect rect, int height) 
            {
                return new Rect(Margin, Margin, rect.width - Margin * 2, height);
            }

            public int PropPx(int px) 
            {
                return (int)(px * (1f + (Screen.width/BaseWidth - 1f) * PropRatio));
            }
        }
        
    //--------

        public void Awake()
        {
            mp = new ModsParam();
            pv = new PixelValues();
            lastScreenSize = new Vector2(Screen.width, Screen.height);
        }

        public void OnLevelWasLoaded(int level)
        {
            sceneLevel = level;

            if (sceneLevel == 9)
            {
                
            }
            else if (sceneLevel == 5) 
            {
                xmlLoad = mp.Init();
                oneLoad = false;
                winRect = pv.PropScreenMH(1f - modsSliderWidth, 0f, modsSliderWidth, 1f);
            }
        }

        public void Update()
        {
            if (sceneLevel == 9)
            {
            }
            else if (sceneLevel == 5 && xmlLoad)
            {
                if (Input.GetKeyDown(KeyCode.F5)) visible = !visible;
            }
            else 
            {
                if(visible) visible = false;
            }
        }

        public void OnGUI()
        {
            if (!visible) return;
            
            GUIStyle winStyle = "box";
            winStyle.fontSize = pv.Font("C1");
            winStyle.alignment = TextAnchor.UpperRight;

            if (sceneLevel == 9)
            {

            }
            else if (sceneLevel == 5 && xmlLoad)
            {
                freeComment = null;
                maid =  GameMain.Instance.CharacterMgr.GetMaid(0);
                if (maid == null) return;

                if (maid.Param != null && maid.Param.status != null && maid.Param.status.free_comment != null)
                {
                    freeComment = maid.Param.status.free_comment;
                }

                if(freeComment != null && Input.GetKey(KeyCode.F4)) {
                    checkFreeComment();
                }

                if (GUI.changed || !oneLoad) getExSaveData();
                    
                if (lastScreenSize != new Vector2(Screen.width, Screen.height))
                {
                    winRect = pv.PropScreenMH(winRect.x, winRect.y, modsSliderWidth, 1f, lastScreenSize);
                    lastScreenSize = new Vector2(Screen.width, Screen.height);
                }
                winRect = GUI.Window(0, winRect, addModsSlider, AddModsSlider.Version, winStyle);
                oneLoad = true;
            }
         }

    //--------


        private void addModsSlider(int winID)
         {
            int mod_num = mp.KeyCount;
            Rect baseRect = pv.InsideRect(this.winRect);
            Rect headerRect = new Rect(baseRect.x, baseRect.y, baseRect.width, pv.Line("H3"));
            Rect scrollRect = new Rect(baseRect.x, baseRect.y + headerRect.height + pv.Margin
                                      ,baseRect.width + pv.PropPx(5), baseRect.height - headerRect.height - pv.Margin);
            Rect conRect = new Rect(0, 0 ,scrollRect.width - pv.Sys_("HScrollBar.Width") - pv.Margin , 0);
            Rect outRect = new Rect();
            GUIStyle lStyle = "label";
            GUIStyle tStyle = "toggle";
            Color color = new Color(1f, 1f, 1f, 0.98f);

            for (int i=0; i<mod_num; i++) 
            {
                string key = mp.sKey[i];

                if (mp.CantDisable.Contains(key)) mp.bEnabled[key] = true;
                if (checkWS(key)) mp.bEnabled[key] = false;

                conRect.height += pv.Line("H1");
                if(mp.sType[key] != "toggle" && mp.bEnabled[key])
                { 
                    for (int j=0; j<mp.ValCount(key); j++) conRect.height += pv.Line("H1");
                    conRect.height += pv.Margin * 2;
                }
                else conRect.height += pv.Margin;
            }

            lStyle.normal.textColor = color;
            tStyle.normal.textColor = color;
            lStyle.fontSize = pv.Font("H3");
            drawWinHeader(headerRect, "Mods Slider", lStyle);

            // スクロールビュー
            scrollViewVector = GUI.BeginScrollView(scrollRect, scrollViewVector, conRect);

            // 各modスライダー
            outRect.Set(0, 0, conRect.width, 0);
            for (int i=0; i<mod_num; i++)
            {
                string key = mp.sKey[i];

                //----
                outRect.width = conRect.width;
                outRect.height  = pv.Line("H1");
                color = (mp.bEnabled[key]) ? new Vector4(1f, 1f, 1f, 0.98f) : new Vector4(0.8f, 0.8f, 0.8f, 0.8f);
                lStyle.normal.textColor = color;
                tStyle.normal.textColor = color;
                
                if (mp.CantDisable.Contains(key) || checkWS(key))  // ON/OFFフラグの無いmodとWIDESLIDER無効化状態でのWS必須modはトグル非表示
                {
                    string s = mp.sDescription[key]+" ("+key+")";
                    lStyle.fontSize = pv.Font("H1");
                    if (checkWS(key))
                    {
                        s = "WIDESLIDER必須:" + s;
                        lStyle.normal.textColor = new Vector4(0.8f, 0.1f, 0.1f, 0.9f);

                    }
                    GUI.Label(outRect, s, lStyle);
                }
                else
                {
                    tStyle.fontSize = pv.Font("H1");
                    mp.bEnabled[key] = GUI.Toggle(outRect, mp.bEnabled[key], mp.sDescription[key]+" ("+key+")", tStyle);
                }
                outRect.y += outRect.height;

                if (mp.sType[key] == "toggle" || !mp.bEnabled[key]) 
                {
                    outRect.y += pv.Margin;
                    continue;
                }
                //----

                int val_num = mp.ValCount(key);
                for (int j=0; j<val_num; j++)
                {
                    string prop = mp.sPropName[key][j];
                    
                    float value = mp.fValue[key][prop];
                    float vmin = mp.fVmin[key][prop];
                    float vmax = mp.fVmax[key][prop];
                    string label = mp.sLabel[key][prop] +" : "+ value.ToString("F");
                    string vType = mp.sVType[key][prop];

                    outRect.width = conRect.width;
                    outRect.height  = pv.Line("H1");
                    lStyle.fontSize = pv.Font("H1");
                    if (value < vmin) value = vmin;
                    if (value > vmax) value = vmax;
                    if (vType == "scale" && vmin < 1f)
                    {
                        if (vmin < 0f) vmin = 0f;
                        if (value < 0f) value = 0f;

                        float tmpmin = -Mathf.Abs(vmax - 1f);
                        float tmpmax = Mathf.Abs(vmax - 1f);
                        float tmp = (value < 1f) ? tmp = Mathf.Abs((1f-value)/(1f-vmin)) * tmpmin : value - 1f;

                        if(tmp < tmpmin) tmp = tmpmin;
                        if(tmp > tmpmax) tmp = tmpmax;

                        tmp = drawModValueSlider(outRect, tmp, tmpmin, tmpmax, label, lStyle);

                        mp.fValue[key][prop] = (tmp < 0f) ? 1f - tmp/tmpmin * Mathf.Abs(1f-vmin) : 1f + tmp;
                    }
                    else if (vType == "int") 
                    {
                        value = (int)Mathf.Round(value);
                        mp.fValue[key][prop] = (int)Mathf.Round(drawModValueSlider(outRect, value, vmin, vmax, label, lStyle));
                    }
                    else mp.fValue[key][prop] = drawModValueSlider(outRect, value, vmin, vmax, label, lStyle);

                    outRect.y += outRect.height;
                }

                outRect.y += pv.Margin * 2;
            }

            GUI.EndScrollView();
            GUI.DragWindow();
            
            if (GUI.changed || !oneLoad) setExSaveData();
        }

        private void drawWinHeader(Rect rect, string s, GUIStyle style)
        {
            GUI.Label(rect, s, style);
            {
                ;
            }
        }

        private float drawModValueSlider(Rect outRect, float value, float min, float max, string label, GUIStyle lstyle)
        {
            float conWidth = outRect.width;
            
            outRect.width = conWidth * 0.3f;
            GUI.Label(outRect, label, lstyle);
            outRect.x += outRect.width;
            
            outRect.width = conWidth * 0.7f;
            outRect.y += pv.PropPx(5);
            return GUI.HorizontalSlider(outRect, value, min, max);
        }

    //--------

        private void checkFreeComment()
        {
            for(int i=0; i<mp.KeyCount; i++)
            { 
                string key = mp.sKey[i];
            
                Match match = Regex.Match(freeComment, getMatchPattern(key));
                
                if (mp.sType[key] == "toggle")
                {
                     mp.bEnabled[key] = (match.Groups.Count > 1) ? true : false;
                }
                else if (match.Groups.Count > mp.ValCount(key)) 
                {
                    int val_num = mp.ValCount(key);
                    bool tpf = true;

                    for(int j=0; j<val_num; j++) 
                    {
                        float f = 0f;
                        string prop = mp.sPropName[key][j];
                    
                        mp.bEnabled[key] = true;
                        tpf &= Single.TryParse(match.Groups[j + 1].Value, out f);
                        if(tpf) mp.fValue[key][prop] = f;

                    }
                    if(!tpf) mp.bEnabled[key] = false;
                }
                else mp.bEnabled[key] = false;
            }
        }

        private string getMatchPattern(string key)
        {
            if (mp.sType[key] == "toggle") return "#("+ key + ")#";
             
            string s = "#" + key;
            int val_num = mp.ValCount(key);
            
            for(int j=0; j<val_num; j++) 
            {
                string prop = mp.sPropName[key][j];
                s += (j==0) ? "=" : ",";
                
                if(mp.sMatchPattern[key][prop] == "")
                {
                    s += mp.DefMatchPattern;
                }
                else
                {
                    s += mp.sMatchPattern[key][prop];
                }                
            }
            s += "#";
            
            return s;
        }

        private bool checkWS(string key)
        {
            return mp.sKey.Contains("WIDESLIDER") && !mp.bEnabled["WIDESLIDER"] && mp.NeedsWideSlider.Contains(key);
        }

    //--------

        private void getExSaveData()
        {
            for (int i=0; i<mp.KeyCount; i++)
            {
                string key = mp.sKey[i];

                if (mp.IsNotBoolKey.Contains(key)) // name=keyにBool以外数値が入ってるmod用
                {
                    float f = ExSaveData.GetFloat(maid, "CM3D2.MaidVoicePitch", key, float.NaN);
                    if (float.IsNaN(f)) f = mp.fVdefault[key][key + ".value"];

                    mp.fValue[key][key + ".value"] = f;
                }
                else
                {
                    mp.bEnabled[key] = ExSaveData.GetBool(maid, "CM3D2.MaidVoicePitch", key, false);

                    if (mp.sType[key] != "toggle")
                    {
                        for (int j=0; j<mp.ValCount(key); j++)
                        {
                            string prop = mp.sPropName[key][j];

                            float f = ExSaveData.GetFloat(maid, "CM3D2.MaidVoicePitch", mp.sPropName[key][j], float.NaN);
                            if (!float.IsNaN(f)) mp.fValue[key][prop] = f;
                            else mp.fValue[key][prop] = mp.fVdefault[key][prop];
                        }
                    }
                }
            }
            // WideSlider前提modのWideSlider無効時スライダー無効化
            for (int i=0; i<mp.KeyCount; i++) if (checkWS(mp.sKey[i])) mp.bEnabled[mp.sKey[i]] = false;
        }

        private void setExSaveData()
        {
            for (int i=0; i<mp.KeyCount; i++)
            {
                string key = mp.sKey[i];
                
                if (mp.IsNotBoolKey.Contains(key)) // name=keyにBool以外の数値が入ってるmod用
                {
                    ExSaveData.SetFloat(maid, "CM3D2.MaidVoicePitch", key, mp.fValue[key][key + ".value"]);
                }
                else 
                {
                    // ON/OFFフラグの無いmodは name=key value=bool を書き出さない
                    if (!mp.CantDisable.Contains(key)) ExSaveData.SetBool(maid, "CM3D2.MaidVoicePitch", key, mp.bEnabled[key]);
                    
                    if (mp.sType[key] != "toggle")
                    {
                        for (int j=0; j<mp.ValCount(key); j++)
                        {
                            string prop = mp.sPropName[key][j];
                            ExSaveData.SetFloat(maid, "CM3D2.MaidVoicePitch", prop, mp.fValue[key][prop]);
                        }
                    }
                }
            }
        }

    }
}
