using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Linq;
using UnityEngine;
using UnityInjector.Attributes;
using CM3D2.ExternalSaveData.Managed;

namespace CM3D2.AddModsSlider.Plugin
{
    [PluginFilter("CM3D2x64"), PluginFilter("CM3D2x86"), PluginFilter("CM3D2VRx64")]
    [PluginName("CM3D2 AddModsSlider"), PluginVersion("0.1.1.16")]
    public class AddModsSlider : UnityInjector.PluginBase
    {

        #region Constants

        public const string PluginName = "AddModsSlider";
        public const string Version    = "0.1.1.16";
        
        private readonly string WinFileName = Directory.GetCurrentDirectory() + @"\UnityInjector\Config\ModsSliderWin.png";
        private readonly string LogLabel = AddModsSlider.PluginName + " : ";

        #endregion



        #region Variables

        private int   sceneLevel;
        private bool  xmlLoad       = false;
        private bool  visible       = false;
        private bool  initCompleted = false;
        private float fPassedTimeOnLevel = 0f;
        private float fLastInitTime      = 0f;

        private ModsParam mp;

        private Maid maid;
        private GameObject goAMSPanel;
        private GameObject goScrollView;
        private GameObject goScrollPanelGrid;
        private Dictionary<string, UIButton[]> uiOnOffButton = new Dictionary<string, UIButton[]>();
        private Dictionary<string, Dictionary<string, UILabel>> uiValueLable = new Dictionary<string, Dictionary<string, UILabel>>();

        #endregion



        #region Nested classes

        private class ModsParam 
        {
            private readonly string LogLabel = AddModsSlider.PluginName + " : ";

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

            public ModsParam() {}

            public bool Init()
            {
                if(!loadModsParamXML()) 
                {
                    Debug.LogError(LogLabel +"loadModsParamXML() failed.");
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
                return (sType[key].Contains("toggle")) ? true : false;
            }

            public bool IsSlider(string key) 
            {
                return (sType[key].Contains("slider")) ? true : false;
            }

            //--------

            private bool loadModsParamXML()
            {
                if (!File.Exists(XmlFileName)) 
                {
                    Debug.LogError(LogLabel +"\"" + XmlFileName + "\" does not exist.");
                    return false;
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(XmlFileName);

                 XmlNode mods = doc.DocumentElement;
                 XmlFormat = ((XmlElement)mods).GetAttribute("format");
                if (XmlFormat != "1.2")
                {
                    Debug.LogError(LogLabel +""+ AddModsSlider.Version +" requires fomart=\"1.2\" of ModsParam.xml.");
                    return false;
                }
                
                XmlNodeList modNodeS = mods.SelectNodes("/mods/mod");
                if (!(modNodeS.Count > 0)) 
                {
                    Debug.LogError(LogLabel +" \"" + XmlFileName + "\" has no <mod>elements.");
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
                    switch (sType[key])
                    {
                        case "toggle": break;
                        case "toggle,slider": break;
                        default: sType[key] = "slider"; break;
                    }

                    if (!IsSlider(key)) continue;

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
 
        #endregion



        #region MonoBehaviour methods

        public void OnLevelWasLoaded(int level)
        {
            fPassedTimeOnLevel = 0f;

            if (level != sceneLevel && sceneLevel == 5) finalize();

            if (level == 5)
            {
	            fLastInitTime = 0f;
                initCompleted = false;

	            mp      = new ModsParam();
                xmlLoad = mp.Init();
            }
            
            sceneLevel = level;
        }

        public void Update()
        {
            fPassedTimeOnLevel += Time.deltaTime;

            if (sceneLevel == 5 && xmlLoad)
            {
                if (!initCompleted && (fPassedTimeOnLevel - fLastInitTime > 1f))
                { 
                    fLastInitTime = fPassedTimeOnLevel;
                    initCompleted = initialize();
                }
                
                if (!initCompleted) return;

                if (Input.GetKeyDown(KeyCode.F5))
                {
                     visible = !visible;
                     goAMSPanel.SetActive(visible);
                }
           }
        }

        #endregion



        #region Callbacks

        public void OnClickButton()
        {
            string key = UIButton.current.name.Split(':')[1];
            if(UIButton.current.name.Split(':')[0] == "ButtonOn")  mp.bEnabled[key] = true;
            if(UIButton.current.name.Split(':')[0] == "ButtonOff") mp.bEnabled[key] = false;
            toggleButtonColor(uiOnOffButton[key], mp.bEnabled[key]);

            setExSaveData(key);
            
            notifyMaidVoicePitchOnChange();

            // ON/OFF操作付きスライダーはON/OFFボタン押下でスライダー表示/非表示切替
            if (mp.IsSlider(key) || key == "WIDESLIDER")  toggleActiveUIOnClickButton(key);
        }

        public void OnChangeSlider()
        {
            string key   = UIProgressBar.current.name.Split(':')[1];
            string prop  = UIProgressBar.current.name.Split(':')[2];
            float  value = codecSliderValue(key, prop, UIProgressBar.current.value);
            string vType = mp.sVType[key][prop];

            uiValueLable[key][prop].text = value.ToString("F2");
            mp.fValue[key][prop] = value;

            setExSaveData(key, prop);

            notifyMaidVoicePitchOnChange();
        }

        #endregion



        #region Private methods

        private bool initialize()
        {
            maid = GameMain.Instance.CharacterMgr.GetMaid(0);
			if (maid == null) return false;

			if (!initModsSliderNGUI()) return false;

			return true;
		}
		
		private void finalize()
		{
			visible = false;
			mp      = null;

			maid              = null;
			goAMSPanel        = null;
			goScrollView      = null;
			goScrollPanelGrid = null;
			
			uiOnOffButton.Clear();
			uiValueLable.Clear();
		}

        private bool initModsSliderNGUI()
        {
            // コンフィグパネルよりOnOffボタン取得・複製
            GameObject goSysUIRoot = GameObject.Find("__GameMain__/SystemUI Root");
            if (!goSysUIRoot)
            {
                Debug.LogError(LogLabel +"SystemUI Root is not found.");
                return false;
            }
            GameObject goButtonOn  = FindChild(goSysUIRoot, "On");
            GameObject goButtonOff = FindChild(goSysUIRoot, "Off");
            GameObject goButtonOnStock  = null;
            GameObject goButtonOffStock = null;
            if (!goButtonOn || !goButtonOff) 
            {
                Debug.LogError(LogLabel +"goButtonOn/Off is not found.");
                return false;
            }
            else
            {
                goButtonOnStock  = UnityEngine.Object.Instantiate(goButtonOn)  as GameObject;
                goButtonOffStock = UnityEngine.Object.Instantiate(goButtonOff) as GameObject;
                goButtonOnStock.name  = "goButtonOnStock";
                goButtonOffStock.name = "goButtonOffStock";
                if (!goButtonOnStock.GetComponentsInChildren<UIButton>(true)[0] || !goButtonOnStock.GetComponentsInChildren<UIButton>(true)[0])
                {
                    Debug.LogError(LogLabel +"UIButton is not found.");
                    return false;
                }

                EventDelegate.Remove(goButtonOnStock.GetComponentsInChildren<UIButton>(true)[0].onClick, 
                                    new EventDelegate.Callback(BaseMgr<ConfigMgr>.Instance.OnSysButtonShowAlwaysEnabled));
                EventDelegate.Remove(goButtonOffStock.GetComponentsInChildren<UIButton>(true)[0].onClick,
                                    new EventDelegate.Callback(BaseMgr<ConfigMgr>.Instance.OnSysButtonShowAlwaysDisabled));
                goButtonOnStock.SetActive(false);
                goButtonOffStock.SetActive(false);
            }
            

            // スライダープレハブ取得
            UnityEngine.Object prefabSlider = Resources.Load("SceneEdit/MainMenu/Prefab/Slider");
            if (!prefabSlider) 
            {
                Debug.LogError(LogLabel +"Prefab/Slider is not found.");
                return false;
            }

            // ProfileLabelUnitからtrueTypeFontを取得
            UnityEngine.Object prefabProfileLabelUnit = Resources.Load("SceneEdit/Profile/Prefab/ProfileLabelUnit");
            GameObject goProfileLabelUnit = UnityEngine.Object.Instantiate(prefabProfileLabelUnit) as GameObject;
            UILabel componentUILabel = UTY.GetChildObject(goProfileLabelUnit, "Parameter", false).GetComponent<UILabel>();
            Font font = componentUILabel.trueTypeFont;
            UnityEngine.Object.Destroy(goProfileLabelUnit);
            if (!font) 
            {
                Debug.LogError(LogLabel +"trueTypeFont is not found.");
                return false;
            }

            GameObject goUIRoot = GameObject.Find("UI Root");
            GameObject cameraObject    = GameObject.Find("/UI Root/Camera");
            Camera     cameraComponent = cameraObject.GetComponent<Camera>();
            UICamera   nguiCamera      = cameraObject.GetComponent<UICamera>();

            // ヘッダーを取得・複製
            GameObject goHeader = FindChild(goUIRoot, "CategoryTitle");
            if (!goHeader) 
            {
                Debug.LogError(LogLabel +"CategoryTitle is not found.");
                return false;
            }

            // スクロールパネルを取得・複製
            GameObject goScrollPanelSlider = goUIRoot.transform.Find("ScrollPanel-Slider").gameObject;
            if (!goScrollPanelSlider) 
            {
                Debug.LogError(LogLabel +"ScrollPanel-Slider is not found.");
                return false;
            }

            
            // 拡張セーブデータ読込
			Debug.Log(LogLabel +"Loading ExternalSaveData...");
			Debug.Log("----------------ExternalSaveData----------------");
            getExSaveData();
			Debug.Log("------------------------------------------------");
            

            //UIRoot uiRoot = goUIRoot.GetComponent<UIRoot>();
            int rootWidth  = 1920; //uiRoot.manualWidth;
            //int rootHeight = 1080; //uiRoot.manualHeight;Debug.LogWarning(uiRoot.adjustByDPI);
            //int scrlWidth  = 540;
            int scrlHeight = 910;
            int dragTabHWidth = 50;
            int dragTabHeight = 500;
            int adjusterWidth = (1 - Screen.width / rootWidth) * -25;

            float winx  = (rootWidth - 30) * 0.5f ;// adjusterWidth;
            float winy  = (scrlHeight - dragTabHeight) * 0.5f;
            float scrlx = dragTabHWidth * 0.5f + 10;
            float scrly = -winy;

            //float gridCellHeight = 105f;

            // ModsSliderパネル（タイトル部分）をUIRootに追加
            UIPanel AMSPanel = NGUITools.AddChild<UIPanel>(goUIRoot);
            goAMSPanel = AMSPanel.gameObject;
            goAMSPanel.transform.localPosition = new Vector3(winx - scrlx * 0.5f, winy, 1f);
            goAMSPanel.transform.localScale    = new Vector3(1f, 1f, 1f);
            goAMSPanel.transform.rotation      = Quaternion.identity * Quaternion.Euler(0f, 30f, 0f);
            goAMSPanel.name = "AddModsSliderPanel";
            goAMSPanel.AddComponent<UIDragObject>().target = goAMSPanel.transform;
            goAMSPanel.AddComponent<BoxCollider>().isTrigger = true;

            UITexture myTexture       = goAMSPanel.AddComponent<UITexture>();
            myTexture.mainTexture     = LoadTexture(WinFileName, 100, 100);
            myTexture.autoResizeBoxCollider = true;
            myTexture.MakePixelPerfect();

            // コピーしたスクロールパネル（メイン部分）をModsSliderパネルに追加
            GameObject goScrollPanel =  UnityEngine.Object.Instantiate(goScrollPanelSlider) as GameObject;
            SetChild(goAMSPanel, goScrollPanel);
            goScrollPanel.transform.localPosition = new Vector3(scrlx, scrly, 1f);
            goScrollPanel.name = "ScrollPanel";
            goScrollPanel.SetActive(true);

            // UIScrollViewの設定
            goScrollView = FindChild(goScrollPanel, "Scroll View");
            goScrollView.transform.localPosition = new Vector3(-297f, 45f, 0f);
            UIScrollView uiScrollView = goScrollView.GetComponent<UIScrollView>();

            goScrollPanelGrid = FindChild(goScrollPanel, "UIGrid");
            goScrollPanelGrid.GetComponent<UICenterOnChild>().enabled = false;

            // UIGridの設定変更
            UIGrid uiGrid  = goScrollPanelGrid.GetComponent<UIGrid>();
            /*uiGrid.cellHeight      = gridCellHeight;
            uiGrid.pivot           = UIWidget.Pivot.Top;
            uiGrid.hideInactive    = true;
            uiGrid.keepWithinPanel = true;
            //uiGrid.animateSmoothly = true;
            uiGrid.sorting         = UIGrid.Sorting.Custom;
            uiGrid.onCustomSort    = (Comparison<Transform>)this.sortGridByXMLOrder;
            */
            // UITableの設定変更
            uiGrid.enabled = false;
            goScrollPanelGrid.GetComponent<UIGrid>().enabled = false;
            UITable uiTable         = goScrollPanelGrid.AddComponent<UITable>();
            uiTable.pivot           = UIWidget.Pivot.Top;
            uiTable.columns         = 1;
            uiTable.padding         = new Vector2(21f, 10f);
            uiTable.hideInactive    = true;
            uiTable.keepWithinPanel = true;
            uiTable.sorting         = UITable.Sorting.Custom;
            uiTable.onCustomSort    = (Comparison<Transform>)this.sortGridByXMLOrder;
            uiTable.enabled = true;
            
          try{
            // ModsParamの設定に従ってボタン・スライダー追加
            for (int i = 0; i < mp.KeyCount; i++)
            {
                string key = mp.sKey[i];

                if (!mp.bVisible[key]) continue;

                uiOnOffButton[key] = new UIButton[2];
                uiValueLable[key]  = new Dictionary<string, UILabel>();

                string modeDesc = mp.sDescription[key] + " (" + key + ")";
                
                if (mp.IsSlider(key)) 
                {
					UILabel sl = NGUITools.AddChild<UILabel>(goScrollPanelGrid);
					sl.text = "　";
					sl.gameObject.name =  "Spacer:" + key;
					sl.gameObject.transform.localScale = new Vector3(1f, 0.38f, 1f);
				}

				// GridにPanel追加
                UIPanel uiModPanel = NGUITools.AddChild<UIPanel>(goScrollPanelGrid);
                uiModPanel.depth = 10;
                GameObject goModPanel = uiModPanel.gameObject;
                goModPanel.name = "Panel:" + key;
            	goModPanel.transform.localPosition = new Vector3(0f, 0f, 0f);

				// {}＝Panelの子
                {
					// PanelにHeaderLabel追加
					GameObject goHeaderLabel = UnityEngine.Object.Instantiate(goHeader) as GameObject;
	                SetChild(goModPanel, goHeaderLabel);
	                goHeaderLabel.transform.localPosition = new Vector3(0f, 0f, 0f);
	                goHeaderLabel.transform.localScale    = (mp.IsSlider(key)) ? new Vector3(0.8f, 1.33f, 1f) : new Vector3(0.8f, 1f, 1f); 
	                goHeaderLabel.name = "Header:" + key;

					// HeaderLabel設定
	                UILabel uiHeaderLabel = FindChild(goHeaderLabel, "Name").GetComponent<UILabel>();
	                uiHeaderLabel.gameObject.transform.localPosition = new Vector3(-15f, 0f, 0f);
	                uiHeaderLabel.gameObject.transform.localScale    = (mp.IsSlider(key)) ? new Vector3(1.26f, 0.78f, 1f) : new Vector3(1.26f, 1f, 1f);
	                uiHeaderLabel.width        = 450;
	                uiHeaderLabel.alignment    = NGUIText.Alignment.Left;
	                uiHeaderLabel.text         = ((mp.IsSlider(key)) ? "■ " : "") + modeDesc;

					// PanelにOn/OffButton追加
	                if (mp.IsToggle(key))
	                {
	                    goHeaderLabel.transform.localPosition = new Vector3(0f, 50f, 0f);

	                    GameObject goButtonModOn = UnityEngine.Object.Instantiate(goButtonOnStock) as GameObject;
	                    SetChild(goModPanel, goButtonModOn);
	                    goButtonModOn.transform.localPosition = new Vector3(-100f, 0f, 0f);
	                    goButtonModOn.transform.localScale    = new Vector3(0.8f, 0.8f, 1f);
	                    goButtonModOn.name = "ButtonOn:" + key;
	                    goButtonModOn.AddComponent<UIDragScrollView>().scrollView = uiScrollView;
	                    goButtonModOn.SetActive(true);

	                    GameObject goButtonModOff = UnityEngine.Object.Instantiate(goButtonOffStock) as GameObject;
	                    SetChild(goModPanel, goButtonModOff);
	                    goButtonModOff.transform.localPosition = new Vector3(100f, 0f, 0f);
	                    goButtonModOff.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
	                    goButtonModOff.name = "ButtonOff:" + key;
	                    goButtonModOff.AddComponent<UIDragScrollView>().scrollView = uiScrollView;
	                    goButtonModOff.SetActive(true);

	                    uiOnOffButton[key][0] = goButtonModOn.GetComponentsInChildren<UIButton>(true)[0];
	                    uiOnOffButton[key][1] = goButtonModOff.GetComponentsInChildren<UIButton>(true)[0];

	                    EventDelegate.Add(uiOnOffButton[key][0].onClick, new EventDelegate.Callback(this.OnClickButton));
	                    EventDelegate.Add(uiOnOffButton[key][1].onClick, new EventDelegate.Callback(this.OnClickButton));

	                    toggleButtonColor(uiOnOffButton[key], mp.bEnabled[key]);

	                    if (!mp.CheckWS(key)) goModPanel.SetActive(false);
	                }
				}
				
				// GridにSlider追加
                if (mp.IsSlider(key))
                {
					// ボタン付きスライダーなら上部に空白追加

                    for (int j=0; j<mp.ValCount(key); j++)
                    {
                        string prop = mp.sPropName[key][j];

                        if (!mp.bVVisible[key][prop]) continue;

                        float  value = mp.fValue[key][prop];
                        float  vmin  = mp.fVmin[key][prop];
                        float  vmax  = mp.fVmax[key][prop];
                        string label = mp.sLabel[key][prop];
                        string vType = mp.sVType[key][prop];

						// プレハブよりスライダーセットを複製
                        GameObject goModSlider = UnityEngine.Object.Instantiate(prefabSlider) as GameObject;
                        goModSlider.name = "Slider:" + key + ":" + prop;

						// スライダーつまみ操作時はスクロールビュードラッグを無効に設定
                        GameObject goModSliderThumb = FindChild(goModSlider,"Thumb");
                        UIDragScrollView thumbUIDragScrollView = goModSliderThumb.GetComponent<UIDragScrollView>();
                        thumbUIDragScrollView.enabled = false;
                        
						// スライダー設定
                        UISlider uiModSlider = goModSlider.GetComponentsInChildren<UISlider>(true)[0];
                        uiModSlider.value = codecSliderValue(key, prop);
                        if (vType == "int") uiModSlider.numberOfSteps = (int)(vmax - vmin + 1);
                        EventDelegate.Add(uiModSlider.onChange, new EventDelegate.Callback(this.OnChangeSlider));
                        
                        // スライダーラベル設定
                        GameObject goModLabel = FindChild(goModSlider,"Name");
                        goModLabel.transform.localPosition = new Vector3(0f, 0f, 0f);
                        UILabel modLabel = goModLabel.GetComponent<UILabel>();
                        modLabel.width = 450;
                        modLabel.text  = label;

                        // スライダー値ラベル参照取得
                        uiValueLable[key][prop] = FindChild(goModSlider,"Value").GetComponent<UILabel>();

                        // スライダーが有効状態であればGridに追加
						SetChild(goScrollPanelGrid, goModSlider);
                        goModSlider.SetActive( !mp.IsToggle(key) || mp.bEnabled[key] && mp.CheckWS(key) );
                    }
                }
            }
          } catch(Exception ex) { Debug.Log(LogLabel +"initModsSliderNGUI() for-loop "+ ex); }

            uiGrid.repositionNow = true;
            uiScrollView.ResetPosition();
            goAMSPanel.SetActive(false);

            Debug.Log(LogLabel +"Initialization Complete.");
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
                
                return (float)Math.Round((dvalue - dvmin) / (dvmax - dvmin), 1, MidpointRounding.AwayFromZero);
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

        public void toggleActiveUIOnClickButton(string key)
        {
          try{
            bool b = mp.bEnabled[key];

            foreach (Transform trans in goScrollPanelGrid.transform)
            {
				string goType = trans.name.Split(':')[0];
				string goKey  = trans.name.Split(':')[1];
				
				if ( (key == "WIDESLIDER" && mp.bOnWideSlider[goKey]) || (goType == "Slider" && goKey == key) )
					trans.gameObject.SetActive(b);
			}

            //goScrollPanelGrid.GetComponent<UIGrid>().repositionNow = true;
            goScrollPanelGrid.GetComponent<UITable>().Reposition();
          } catch(Exception ex) { Debug.Log(LogLabel +"toggleActiveUIOnClickButton() "+ ex); }
        }

        private void toggleButtonColor(UIButton[] onoff, bool b)
        {
            Color color = onoff[0].defaultColor;
            onoff[0].defaultColor = new Color(color.r, color.g, color.b,  b ? 1f : 0.5f);
            onoff[1].defaultColor = new Color(color.r, color.g, color.b,  b ? 0.5f : 1f);
        }

        private int sortGridByXMLOrder(Transform t1, Transform t2)
        {
          try{
            string type1 = t1.name.Split(':')[0];
            string type2 = t2.name.Split(':')[0];
            string key1 = t1.name.Split(':')[1];
            string key2 = t2.name.Split(':')[1];
            int n = mp.sKey.IndexOf(key1);
            int m = mp.sKey.IndexOf(key2);
            
            //Debug.Log(t1.name +" comp "+ t2.name);

            Dictionary<string, int> order = new Dictionary<string, int>(){ {"Spacer", 0}, {"Panel", 1}, {"Header", 2}, {"Slider", 3}   };
            
            if (n == m) 
            {
                if (type1 == "Slider" && type2 == "Slider")
                {
                    int l = Array.IndexOf(mp.sPropName[key1], t1.name.Split(':')[2]);
                    int k = Array.IndexOf(mp.sPropName[key2], t2.name.Split(':')[2]);
                    
                    return l - k;
                }
                else return order[type1] - order[type2];
            }
            else return n - m;

          } catch(Exception ex) { Debug.Log(LogLabel +"sortGridByXMLOrder() "+ ex); return 0; }
        }

        private Texture2D LoadTexture(string path, int w, int h)
        {
            if (!File.Exists(path)) 
            {
                Debug.LogError(LogLabel +"\"" + path + "\" does not exist.");
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

        //--------
        
        private void notifyMaidVoicePitchOnChange()
        {
            this.gameObject.SendMessage("MaidVoicePitch_UpdateSliders");
        }

        private void getExSaveData()
        {
            string plugin = "CM3D2.MaidVoicePitch";
            for (int i=0; i<mp.KeyCount; i++)
            {
                string key = mp.sKey[i];
                
                if (mp.IsToggle(key))
                {
                    mp.bEnabled[key] = ExSaveData.GetBool(maid, plugin, key, false);
                    Debug.Log( string.Format("{0,-32} = {1,-16}", key, mp.bEnabled[key]) );
                }

                if (mp.IsSlider(key))
                {
                    for (int j=0; j<mp.ValCount(key); j++)
                    {
                        string prop = mp.sPropName[key][j];
                        float f = ExSaveData.GetFloat(maid, plugin, prop, float.NaN);
                        mp.fValue[key][prop] =  float.IsNaN(f) ? mp.fVdef[key][prop] : f;

                        Debug.Log( string.Format("{0,-32} = {1:f}", prop,　mp.fValue[key][prop]) );
                    }
                    if (!mp.IsToggle(key)) mp.bEnabled[key] = true;
                }
            }
        }

        private void setExSaveData()
        {
            for (int i=0; i<mp.KeyCount; i++) setExSaveData(mp.sKey[i]);
        }

        private void setExSaveData(string key)
        {
            string plugin = "CM3D2.MaidVoicePitch";

            if (mp.IsToggle(key))
            {
                ExSaveData.SetBool(maid, plugin, key, mp.bEnabled[key]);
            }
            
            if (mp.IsSlider(key))
            {
                for (int j=0; j<mp.ValCount(key); j++) setExSaveData(key, mp.sPropName[key][j]);
            }
        }

        private void setExSaveData(string key, string prop)
        {
            string plugin = "CM3D2.MaidVoicePitch";

            float value = (float)Math.Round(mp.fValue[key][prop], 3, MidpointRounding.AwayFromZero);

            ExSaveData.SetFloat(maid, plugin, prop, value);
        }

        #endregion



        #region Utility methods

        internal static GameObject FindChild(GameObject go, string s)
        {
            if (go == null) return null;
            GameObject target = null;
            
            foreach (Transform tc in go.transform)
            {
                if (tc.gameObject.name == s) return tc.gameObject;
                target = FindChild(tc.gameObject, s);
                if (target) return target;
            } 
            
            return null;
        }

        internal static void SetChild(GameObject parent, GameObject child)
        {
            child.layer                   = parent.layer;
            child.transform.parent        = parent.transform;
            child.transform.localPosition = Vector3.zero;
            child.transform.localScale    = Vector3.one;
            child.transform.rotation      = Quaternion.identity;
        }

        internal static void WriteTrans(Transform t, int level, StreamWriter writer)
        {
            if (level == 0) writer = new StreamWriter(@".\"+ t.name +@".txt", false);
            if (writer == null) return;
            
            string s = "";
            for(int i=0; i<level; i++) s+="    ";
            writer.WriteLine(s + level +","+t.name);
            foreach (Transform tc in t)
            {
                WriteTrans(tc, level+1, writer);
            }

            if (level == 0) writer.Close();
        }

        internal static void WriteComponent(GameObject go)
        {
            Component[] compos = go.GetComponents<Component>();
            foreach(Component c in compos){ Debug.Log(go.name +":"+ c.GetType().Name); }
        }

        #endregion

    }
}

