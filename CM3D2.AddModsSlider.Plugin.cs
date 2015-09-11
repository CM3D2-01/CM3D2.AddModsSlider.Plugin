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
    PluginVersion("0.1.1.9")]
    public class AddModsSlider : UnityInjector.PluginBase
    {
        public const string Version = "0.1.1.9";
        public readonly string WinFileName = Directory.GetCurrentDirectory() + @"\UnityInjector\Config\ModsSliderWin.png";

        private int sceneLevel;
        private bool  xmlLoad       = false;
        private bool  visible       = false;
        private bool  initCompleted = false;
        private float fPassedTimeOnLevel = 0f;
        private float fLastInitTime      = 0f;
        private ModsParam mp;
        private Maid maid;

        GameObject goAMSPanel;
        Dictionary<string, UIButton[]> uiOnOffButton = new Dictionary<string, UIButton[]>();
        Dictionary<string, Dictionary<string, UILabel>> uiValueLable = new Dictionary<string, Dictionary<string, UILabel>>();

        private class ModsParam 
        {
            public readonly string DefMatchPattern = @"([-+]?[0-9]*\.?[0-9]+)";
            public readonly string XmlFileName = Directory.GetCurrentDirectory() + @"\UnityInjector\Config\ModsParam.xml";
            
            public string XmlFormat;
            public List<string> sKey = new List<string>();

            public Dictionary<string, bool>     bEnabled       = new Dictionary<string, bool>();
            public Dictionary<string, string>   sDescription   = new Dictionary<string, string>();
            public Dictionary<string, string>   sType          = new Dictionary<string, string>();
            public Dictionary<string, bool>     bOnWideSlider  = new Dictionary<string, bool>();
            public Dictionary<string, bool>     bVisible       = new Dictionary<string, bool>();

            public Dictionary<string, string[]> sPropName      = new Dictionary<string, string[]>();
            public Dictionary<string, Dictionary<string, float>>  fValue        = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, float>>  fVmin         = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, float>>  fVmax         = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, float>>  fVdef         = new Dictionary<string, Dictionary<string, float>>();
            public Dictionary<string, Dictionary<string, string>> sVType        = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Dictionary<string, string>> sLabel        = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Dictionary<string, string>> sMatchPattern = new Dictionary<string, Dictionary<string, string>>();
            public Dictionary<string, Dictionary<string, bool>>   bVVisible     = new Dictionary<string, Dictionary<string, bool>>();

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
                foreach(string key in sKey) CheckWS(key);
                
                return true;
            }

            public bool CheckWS(string key) 
            {
                return !bOnWideSlider[key] || (sKey.Contains("WIDESLIDER") && bEnabled["WIDESLIDER"]);
            }

            public bool IsToggle(string key) 
            {
                return (sType[key] == "toggle") ? true : false;
            }

        //--------

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
                if (XmlFormat != "1.2")
                {
                    Debug.LogError("AddModsSlider : "+ AddModsSlider.Version +" requires fomart=\"1.2\" of ModsParam.xml.");
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

                    bool b = false;
                    bEnabled[key]       = false;
                    sDescription[key]   = ((XmlElement)modNode).GetAttribute("description");
                    bOnWideSlider[key]  = (Boolean.TryParse(((XmlElement)modNode).GetAttribute("on_wideslider"),  out b)) ? b : false;
                    bVisible[key]       = (Boolean.TryParse(((XmlElement)modNode).GetAttribute("visible"),        out b)) ? b : true;
                    
                    sType[key] = ((XmlElement)modNode).GetAttribute("type");

                    if (sType[key] == "") sType[key] = "slider";
                    if (IsToggle(key)) continue;

                    XmlNodeList valueNodeS = ((XmlElement)modNode).GetElementsByTagName("value");
                    if (!(valueNodeS.Count > 0)) continue;
                    
                    sPropName[key]     = new string[valueNodeS.Count];
                    fValue[key]        = new Dictionary<string, float>();
                    fVmin[key]         = new Dictionary<string, float>();
                    fVmax[key]         = new Dictionary<string, float>();
                    fVdef[key]         = new Dictionary<string, float>();
                    sVType[key]        = new Dictionary<string, string>();
                    sLabel[key]        = new Dictionary<string, string>();
                    sMatchPattern[key] = new Dictionary<string, string>();
                    bVVisible[key]     = new Dictionary<string, bool>();
                    
                    // value属性
                    int j = 0;
                    foreach (XmlNode valueNode in valueNodeS)
                    {
                        float x = 0f;
                        
                        string prop = ((XmlElement)valueNode).GetAttribute("prop_name");
                        if (prop != "" && Array.IndexOf(sPropName[key], prop) < 0 )
                        {
                            sPropName[key][j] = prop;
                        }
                        else
                        {    
                            sKey.Remove(key);
                            break;
                        }
                        
                        sVType[key][prop] = ((XmlElement)valueNode).GetAttribute("type");
                        switch (sVType[key][prop])
                        {
                            case "num":   break;
                            case "scale": break;
                            case "int" :  break;
                            default : sVType[key][prop] = "num"; break;
                        }

                        fVmin[key][prop] = Single.TryParse(((XmlElement)valueNode).GetAttribute("min"),     out x) ? x : 0f;
                        fVmax[key][prop] = Single.TryParse(((XmlElement)valueNode).GetAttribute("max"),     out x) ? x : 0f;
                        fVdef[key][prop] = Single.TryParse(((XmlElement)valueNode).GetAttribute("default"), out x) ? x : (sVType[key][prop] =="scale") ? 1f : 0f;
                        fValue[key][prop] = fVdef[key][prop];

                        sLabel[key][prop]        = ((XmlElement)valueNode).GetAttribute("label");
                        sMatchPattern[key][prop] = ((XmlElement)valueNode).GetAttribute("match_pattern");
                        bVVisible[key][prop]     = (Boolean.TryParse(((XmlElement)valueNode).GetAttribute("visible"), out b)) ? b : true;

                        j++;
                    }
                    if (j == 0) sKey.Remove(key);
                }
                
                return true;
            }
        }

    //--------

        public void Awake()
        {
            mp = new ModsParam();
        }

        public void OnLevelWasLoaded(int level)
        {
            sceneLevel = level;
            visible = false;
            fPassedTimeOnLevel = 0f;
            
            if (sceneLevel == 5)
            {
                initCompleted = false;
                xmlLoad = mp.Init();
            }
        }

        public void Update()
        {
            fPassedTimeOnLevel += Time.deltaTime;

            if (sceneLevel == 5 && xmlLoad)
            {
                if (!initCompleted && (fPassedTimeOnLevel - fLastInitTime > 1f))
                { 
                    fLastInitTime = fPassedTimeOnLevel;
                       maid =  GameMain.Instance.CharacterMgr.GetMaid(0);
                    if (maid == null) return;
                    initCompleted = initModsSliderNGUI();
                }
                if (!initCompleted) return;

                if (Input.GetKeyDown(KeyCode.F5))
                {
                     visible = !visible;
                     goAMSPanel.SetActive(visible);
                }
            }
        }

        public void onClickButton()
        {
            string key = UIButton.current.name.Split(':')[1];
            if(UIButton.current.name.Split(':')[0] == "ButtonOn")  mp.bEnabled[key] = true;
            if(UIButton.current.name.Split(':')[0] == "ButtonOff") mp.bEnabled[key] = false;
            toggleButtonColor(uiOnOffButton[key], mp.bEnabled[key]);

            setExSaveData(key);
        }

        public void onChangeSlider()
        {
            string key   = UIProgressBar.current.name.Split(':')[1];
            string prop  = UIProgressBar.current.name.Split(':')[2];
            float  value = codecSliderValue(key, prop, UIProgressBar.current.value);
            string vType = mp.sVType[key][prop];

            uiValueLable[key][prop].text = value.ToString("F2");
            mp.fValue[key][prop] = value;

            setExSaveData(key, prop);
        }

    //--------

        private bool initModsSliderNGUI()
        {
            GameObject goSysUIRoot = GameObject.Find("__GameMain__/SystemUI Root");
            BaseMgr<ConfigMgr>.Instance.OpenConfigPanel();
            GameObject goButtonOn  = findChild(goSysUIRoot, "On");
            GameObject goButtonOff = findChild(goSysUIRoot, "Off");
            if (!goButtonOn || !goButtonOff) 
            {
                Debug.LogError("On or Off Button is not found.");
                BaseMgr<ConfigMgr>.Instance.CloseConfigPanel();
                return false;
            }
            BaseMgr<ConfigMgr>.Instance.CloseConfigPanel();

            UnityEngine.Object prefabSlider = Resources.Load("SceneEdit/MainMenu/Prefab/Slider");
            if (!prefabSlider) 
            {
                Debug.LogError("Prefab/Slider is not found.");
                return false;
            }

            UnityEngine.Object prefabProfileLabelUnit = Resources.Load("SceneEdit/Profile/Prefab/ProfileLabelUnit");
            GameObject goProfileLabelUnit = UnityEngine.Object.Instantiate(prefabProfileLabelUnit) as GameObject;
            UILabel componentUILabel = UTY.GetChildObject(goProfileLabelUnit, "Parameter", false).GetComponent<UILabel>();
            Font font = componentUILabel.trueTypeFont;
            UnityEngine.Object.Destroy(goProfileLabelUnit);
            if (!font) 
            {
                Debug.LogError("trueTypeFont is not found.");
                return false;
            }

            GameObject goUIRoot = GameObject.Find("UI Root");
            GameObject goHeader = findChild(goUIRoot, "CategoryTitle");
            if (!goHeader) 
            {
                Debug.LogError("CategoryTitle is not found.");
                return false;
            }

            getExSaveData();

            float winx = (Screen.width - 20) * 0.5f;
            float winy = 250f;
            float winz = 100f;
            float winw = 100f;

            UIPanel AMSPanel = NGUITools.AddChild<UIPanel>(goUIRoot);
            AMSPanel.transform.localPosition = new Vector3(winx, winy, 0f);
            AMSPanel.transform.localScale    = new Vector3(1f, 1f, 1f);
            AMSPanel.transform.rotation      = Quaternion.identity * Quaternion.Euler(0f, 0f, 0f);
            AMSPanel.SetRect(winx, winy, winz, winw);
            AMSPanel.name = "AddModsSliderPanel";
            goAMSPanel = AMSPanel.gameObject;
            goAMSPanel.AddComponent<UIDragObject>().target = goAMSPanel.transform;;
            goAMSPanel.AddComponent<BoxCollider>().isTrigger = true;

            UITexture myTexture       = goAMSPanel.AddComponent<UITexture>();
            myTexture.mainTexture     = LoadTexture(WinFileName, 100, 100);
            myTexture.autoResizeBoxCollider = true;
            myTexture.MakePixelPerfect();

            string panelName = "ScrollPanel-Slider";
            GameObject goScrollPanelSlider = goUIRoot.transform.Find(panelName).gameObject; 
            GameObject goScrollPanel =  UnityEngine.Object.Instantiate(goScrollPanelSlider) as GameObject;
            goScrollPanel.layer                   = goAMSPanel.layer;
            goScrollPanel.transform.parent        = goAMSPanel.transform;
            goScrollPanel.transform.localPosition = new Vector3(30f, -250f, 0f);
            goScrollPanel.transform.localScale    = Vector3.one;
            goScrollPanel.transform.localRotation = Quaternion.identity;
            goScrollPanel.name = "ScrollPanel";
            goScrollPanel.SetActive(true);

            UIScrollView uiScrollView = findChild(goScrollPanel, "Scroll View").GetComponent<UIScrollView>();

            GameObject goScrollPanelGrid = findChild(goScrollPanel, "UIGrid");
            UIGrid uiGrid = goScrollPanelGrid.GetComponent<UIGrid>();
            uiGrid.cellHeight = 100f;

            for (int i=0; i<mp.KeyCount; i++)
            {
                string key = mp.sKey[i];

                if (!mp.bVisible[key]) continue;
                if (!mp.CheckWS(key))  continue;

                uiOnOffButton[key] = new UIButton[2];
                uiValueLable[key] = new Dictionary<string, UILabel>();

                string modeDesc = mp.sDescription[key] +" ("+key+")";

                if (mp.IsToggle(key)) 
                {
                    UIPanel uiModPanel = NGUITools.AddChild<UIPanel>(goScrollPanelGrid);
                    uiModPanel.depth = 10;
                    GameObject goModPanel = uiModPanel.gameObject;
                    goModPanel.transform.localScale    = new Vector3(1f, 1f, 1f);
                    goModPanel.name   = "Panel:"+ key;

                    GameObject goHeaderLabel = UnityEngine.Object.Instantiate(goHeader) as GameObject;
                    goHeaderLabel.layer                   = goModPanel.layer;
                    goHeaderLabel.transform.parent        = goModPanel.transform;
                    goHeaderLabel.transform.localPosition = new Vector3(0f, 25f, 0f);
                    goHeaderLabel.transform.localScale    = new Vector3(0.8f, 1f, 1f);
                    goHeaderLabel.transform.rotation      = Quaternion.identity;
                    goHeaderLabel.name                    = "Header:" + key;
                    UILabel myLabel = findChild(goHeaderLabel, "Name").GetComponent<UILabel>();
                    myLabel.gameObject.transform.localPosition = new Vector3(10f, 0f, 0f);
                    myLabel.gameObject.transform.localScale    = new Vector3(1.25f, 1f, 1f);
                    myLabel.width        = 500;
                    myLabel.trueTypeFont = font;
                    myLabel.alignment    = NGUIText.Alignment.Left;
                    myLabel.multiLine    = true;
                    myLabel.text         = modeDesc;

                    GameObject goButtonModOn = UnityEngine.Object.Instantiate(goButtonOn) as GameObject;
                    goButtonModOn.layer                   = goModPanel.layer;
                    goButtonModOn.transform.parent        = goModPanel.transform;
                    goButtonModOn.transform.localPosition = new Vector3(-100f, -25f, 0f);
                    goButtonModOn.transform.localScale    = new Vector3(0.8f, 0.8f, 1f);
                    goButtonModOn.transform.rotation      = Quaternion.identity;
                    goButtonModOn.name                    = "ButtonOn:" + key;
                    goButtonModOn.AddComponent<UIDragScrollView>().scrollView = uiScrollView;

                    GameObject goButtonModOff = UnityEngine.Object.Instantiate(goButtonOff) as GameObject;
                    goButtonModOff.layer                   = goModPanel.layer;
                    goButtonModOff.transform.parent        = goModPanel.transform;
                    goButtonModOff.transform.localPosition = new Vector3(100f, -25f, 0f);
                    goButtonModOff.transform.localScale    = new Vector3(0.8f, 0.8f, 1f);
                    goButtonModOff.transform.rotation      = Quaternion.identity;
                    goButtonModOff.name                    = "ButtonOff:" + key;
                    goButtonModOff.AddComponent<UIDragScrollView>().scrollView = uiScrollView;
                    
                    uiOnOffButton[key][0] = goButtonModOn.GetComponentsInChildren<UIButton>(true)[0];
                    uiOnOffButton[key][1] = goButtonModOff.GetComponentsInChildren<UIButton>(true)[0];
                    
                    EventDelegate.Remove(uiOnOffButton[key][0].onClick, new EventDelegate.Callback(BaseMgr<ConfigMgr>.Instance.OnSysButtonShowAlwaysEnabled));
                    EventDelegate.Remove(uiOnOffButton[key][1].onClick, new EventDelegate.Callback(BaseMgr<ConfigMgr>.Instance.OnSysButtonShowAlwaysDisabled));
                    EventDelegate.Add(uiOnOffButton[key][0].onClick, new EventDelegate.Callback(this.onClickButton));
                    EventDelegate.Add(uiOnOffButton[key][1].onClick, new EventDelegate.Callback(this.onClickButton));

                    toggleButtonColor(uiOnOffButton[key], mp.bEnabled[key]);
                    continue;
                }

                GameObject goHeaderLabelS = UnityEngine.Object.Instantiate(goHeader) as GameObject;
                goHeaderLabelS.layer                   = goScrollPanelGrid.layer;
                goHeaderLabelS.transform.parent        = goScrollPanelGrid.transform;
                goHeaderLabelS.transform.localPosition = new Vector3(0f, -65f, 0f);
                goHeaderLabelS.transform.localScale    = new Vector3(0.8f, 1.25f, 1f);
                goHeaderLabelS.transform.rotation      = Quaternion.identity;
                goHeaderLabelS.name                    = "Header:" + key;
                UILabel myLabelS = findChild(goHeaderLabelS, "Name").GetComponent<UILabel>();
                myLabelS.gameObject.transform.localPosition = new Vector3(50f, 0f, 0f);
                myLabelS.gameObject.transform.localScale    = new Vector3(1.25f*1.1f, 0.8f*1.1f, 1f);
                myLabelS.width        = 500;
                myLabelS.trueTypeFont = font;
                myLabelS.alignment    = NGUIText.Alignment.Left;
                myLabelS.multiLine    = true;
                myLabelS.text         = "■ " +modeDesc;

                for (int j=0; j<mp.ValCount(key); j++)
                {
                    string prop = mp.sPropName[key][j];

                    if (!mp.bVVisible[key][prop]) continue;

                    float  value = mp.fValue[key][prop];
                    float  vmin  = mp.fVmin[key][prop];
                    float  vmax  = mp.fVmax[key][prop];
                    string label = mp.sLabel[key][prop];
                    string vType = mp.sVType[key][prop];

                    GameObject goModSlider = UnityEngine.Object.Instantiate(prefabSlider) as GameObject;
                    goModSlider.layer                   = goScrollPanelGrid.layer;
                    goModSlider.transform.parent        = goScrollPanelGrid.transform;
                    goModSlider.transform.localPosition = new Vector3(0f, 0f, 0f);
                    goModSlider.transform.localScale    = new Vector3(1f, 1f, 1f);
                    goModSlider.transform.rotation      = Quaternion.identity;
                    goModSlider.name                    = "Slider:" + key + ":" + prop;

                    GameObject goModSliderThumb = findChild(goModSlider,"Thumb");
                    UIDragScrollView thumbUIDragScrollView = goModSliderThumb.GetComponent<UIDragScrollView>();
                    thumbUIDragScrollView.enabled = false;
                    
                    UISlider uiModSlider = goModSlider.GetComponentsInChildren<UISlider>(true)[0];
                    uiModSlider.value = codecSliderValue(key, prop);
                    if (vType == "int") uiModSlider.numberOfSteps = (int)(vmax - vmin + 1);
                    EventDelegate.Add(uiModSlider.onChange, new EventDelegate.Callback(this.onChangeSlider));
                    
                    GameObject goModLabel = findChild(goModSlider,"Name");
                    goModLabel.transform.localPosition = new Vector3(-60f, 0f, 0f);
                    UILabel modLabel = goModLabel.GetComponent<UILabel>();
                    modLabel.text  = label;
                    modLabel.width = 350;
                    uiValueLable[key][prop] = findChild(goModSlider,"Value").GetComponent<UILabel>();
                }
            }
            goScrollPanelGrid.GetComponent<UIGrid>().Reposition();
            goAMSPanel.SetActive(false);
            Debug.Log("AddModSlider : Completed initialization.");
            return true;
        }

        private float codecSliderValue(string key, string prop)
        {
            float value = mp.fValue[key][prop];
            float vmin = mp.fVmin[key][prop];
            float vmax = mp.fVmax[key][prop];
            string vType = mp.sVType[key][prop];

            if (value < vmin) value = vmin;
            if (value > vmax) value = vmax;

            if (vType == "scale" && vmin < 1f)
            {
                if (vmin < 0f) vmin = 0f;
                if (value < 0f) value = 0f;

                return (value < 1f) ? (value - vmin)/(1f - vmin) * 0.5f : 0.5f + (value - 1f)/(vmax - 1f) * 0.5f;
            }
            else if (vType == "int") 
            {
                decimal dvalue = (decimal)value;
                decimal dvmin  = (decimal)vmin;
                decimal dvmax  = (decimal)vmax;
                
                return (float)Math.Round((dvalue - dvmin) / (dvmax - dvmin), 2, MidpointRounding.AwayFromZero);
            }
            else 
            {
                return (value - vmin) / (vmax - vmin);
            }
        }

        private float codecSliderValue(string key, string prop, float value)
        {
            float vmin = mp.fVmin[key][prop];
            float vmax = mp.fVmax[key][prop];
            string vType = mp.sVType[key][prop];

            if (value < 0f) value = 0f;
            if (value > 1f) value = 1f;
            if (vType == "scale" && vmin < 1f)
            {
                if (vmin < 0f) vmin = 0f;
                if (value < 0f) value = 0f;
                
                return (value < 0.5f) ?  vmin + (1f - vmin) * value * 2f :  1 + (vmax - 1f) * (value - 0.5f) * 2;
            }
            else if (vType == "int") 
            {
                decimal dvalue = (decimal)value;
                decimal dvmin  = (decimal)vmin;
                decimal dvmax  = (decimal)vmax;

                return (float)Math.Round(vmin + (vmax - vmin) * value, 0, MidpointRounding.AwayFromZero);
            }
            else 
            {
                return vmin + (vmax - vmin) * value;
            }
        }

        private void toggleButtonColor(UIButton[] onoff, bool b)
        {
            Color color = onoff[0].defaultColor;
            onoff[0].defaultColor = new Color(color.r, color.g, color.b,  b ? 1f : 0.5f);
            onoff[1].defaultColor = new Color(color.r, color.g, color.b,  b ? 0.5f : 1f);
        }

    //--------

        private void getExSaveData()
        {
            string plugin = "CM3D2.MaidVoicePitch";
            for (int i=0; i<mp.KeyCount; i++)
            {
                string key = mp.sKey[i];
                
                if (mp.IsToggle(key))
                {
                    mp.bEnabled[key] = ExSaveData.GetBool(maid, plugin, key, false);
                    Debug.Log("AddModSlider : getExSaveData() <prop name=\""+ key + "\" value=\""+ mp.bEnabled[key]  +"\">");
                }
                else
                {
                    for (int j=0; j<mp.ValCount(key); j++)
                    {
                        string prop = mp.sPropName[key][j];
                        float f = ExSaveData.GetFloat(maid, plugin, prop, float.NaN);
                        mp.fValue[key][prop] =  float.IsNaN(f) ? mp.fVdef[key][prop] : f;
                        mp.bEnabled[key] = true;

                        Debug.Log("AddModSlider : getExSaveData() <prop name=\""+ prop + "\" value=\""+ mp.fValue[key][prop] +"\">");
                    }
                }
            }
        }

        private void setExSaveData()
        {
            string plugin = "CM3D2.MaidVoicePitch";
            for (int i=0; i<mp.KeyCount; i++)
            {
                string key = mp.sKey[i];
                
                if (mp.IsToggle(key))
                {
                    ExSaveData.SetBool(maid, plugin, key, mp.bEnabled[key]);
                }
                else 
                {
                    for (int j=0; j<mp.ValCount(key); j++)
                    {
                        string prop = mp.sPropName[key][j];
                        float value = (float)Math.Round(mp.fValue[key][prop], 3, MidpointRounding.AwayFromZero);
                        ExSaveData.SetFloat(maid, plugin, prop, value);
                    }
                }
            }
        }

        private void setExSaveData(string key)
        {
            string plugin = "CM3D2.MaidVoicePitch";

            if (mp.IsToggle(key))
            {
                ExSaveData.SetBool(maid, plugin, key, mp.bEnabled[key]);
            }
            else
            {
                for (int j=0; j<mp.ValCount(key); j++)
                {
                    string prop = mp.sPropName[key][j];
                    float value = (float)Math.Round(mp.fValue[key][prop], 3, MidpointRounding.AwayFromZero);
                    ExSaveData.SetFloat(maid, plugin, prop, value);
                }
            }
        }

        private void setExSaveData(string key, string prop)
        {
            string plugin = "CM3D2.MaidVoicePitch";

            float value = (float)Math.Round(mp.fValue[key][prop], 3, MidpointRounding.AwayFromZero);
            ExSaveData.SetFloat(maid, plugin, prop, value);
        }

        Texture2D LoadTexture(string path, int w, int h)
        {
            if (!File.Exists(path)) 
            {
                Debug.LogError("AddModsSlider : \"" + path + "\" does not exist.");
                return null;
            }

            Texture2D texture2D = new Texture2D(w, h);
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            BinaryReader binaryReader = new BinaryReader(fileStream);
            byte[] bytes = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);
            binaryReader.Close();
            texture2D.LoadImage(bytes);
             
            return texture2D;
        }

    //----
    
        internal static GameObject findChild(GameObject go, string s)
        {
            if (go == null) return null;
            GameObject target = null;
            
            foreach (Transform tc in go.transform)
            {
                if (tc.gameObject.name == s) return tc.gameObject;
                target = findChild(tc.gameObject, s);
                if (target) return target;
            } 
            
            return null;
        }
    }
}


