using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using UnityEngine;
using Newtonsoft.Json;

namespace RDCStockManagerRebirth
{
    public class StockManagerUI : MonoBehaviour
    {
        private UIDocument _uiDocument;
        public VisualElement rootDocument;

        private bool _isDragging = false;
        private Vector2 _dragOffset;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            rootDocument = _uiDocument.rootVisualElement;
            Main.properties = RDCSaver.CargarDatos<Properties>(RDCSaver.rutaArchivo);

            rootDocument.RegisterCallback<MouseDownEvent>(OnMouseDown);
            rootDocument.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            rootDocument.RegisterCallback<MouseUpEvent>(OnMouseUp);
        }

        void Start()
        {
            rootDocument.Q<Button>("General_Btn").clicked += () => ChangeToolBar(0);
            rootDocument.Q<Button>("Sort_Btn").clicked += () => ChangeToolBar(1);
            rootDocument.Q<Button>("Inputs_Btn").clicked += () => ChangeToolBar(2);

            ConfigureGeneralTab();
            ConfigureSortTab();
            ConfigureInputsTab();

            VisualElement settingPanel = rootDocument.Q<VisualElement>("Menu");
            settingPanel.style.display = DisplayStyle.None;

            rootDocument.Q<Button>("Save_Btn").clicked += () =>
            {
                settingPanel.style.display = DisplayStyle.None;
                RDCSaver.GuardarDatos<Properties>(RDCSaver.rutaArchivo, Main.properties);
                Main.UpdateUserSettings();
            };
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            _isDragging = true;
            _dragOffset = evt.localMousePosition;
            evt.StopPropagation();
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (_isDragging)
            {
                Vector2 newPosition = evt.mousePosition - _dragOffset;
                rootDocument.style.position = Position.Absolute;
                rootDocument.style.left = newPosition.x;
                rootDocument.style.top = newPosition.y;
                rootDocument.style.width = Length.Percent(100);
                rootDocument.style.height = Length.Percent(100);
            }
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            _isDragging = false;
        }

        private void ConfigureGeneralTab()
        {
            ConfigureSlider("Store_OFS", Main.properties.minPercentageDisplay,value => {
                Main.properties.minPercentageDisplay = value;
            });

            ConfigureSlider("Store_LS", Main.properties.maxPercentageDisplay, value => { Main.properties.maxPercentageDisplay = value; });
            ConfigureSlider("Wharehouse_OFS", Main.properties.minItemStorage, value => { Main.properties.minItemStorage = value; });
            ConfigureSlider("Wharehouse_LS", Main.properties.maxItemStorage, value => { Main.properties.maxItemStorage = value; });

            ConfigureToggle("BoxMode", Main.properties.changeType, value => Main.properties.changeType = value);
            ConfigureToggle("RealBox", Main.properties.realBoxesMode, value => Main.properties.realBoxesMode = value);
            ConfigureToggle("showMaxCapacity", Main.properties.showMaxCapacity, value => Main.properties.showMaxCapacity = value);
        }

        private void ConfigureSlider(string elementName, float initialValue, Action<float> onChange)
        {
            VisualElement root = rootDocument.Q<VisualElement>(elementName);
            Slider slider = root.Q<Slider>("Slider");
            UnityEngine.UIElements.Label valueLabel = root.Q<UnityEngine.UIElements.Label>("Value");

            slider.highValue = 100;
            slider.value = initialValue;
            valueLabel.text = $"{initialValue:F2} %";

            slider.RegisterCallback<ChangeEvent<float>>(evt =>
            {
                valueLabel.text = $"{evt.newValue:F2} %";
                onChange(evt.newValue);
            });
        }

        private void ConfigureToggle(string elementName, bool initialValue, Action<bool> onChange)
        {
            Toggle toggle = rootDocument.Q<Toggle>(elementName);
            toggle.value = initialValue;
            toggle.RegisterCallback<ChangeEvent<bool>>(evt => onChange(evt.newValue));
        }

        private void ConfigureSortTab()
        {
            ConfigureDropdown("SortOrder", new List<string> { "Ascending", "Descending", "Default" }, Main.properties.sortOrder, value =>
            {
                switch (value)
                {
                    case "Ascending":
                        Main.properties.sortOrder = 0;
                        break;
                    case "Descending":
                        Main.properties.sortOrder = 1;
                        break;
                    case "Default":
                        Main.properties.sortOrder = 2;
                        break;
                }
            });

            ConfigureDropdown("SortBy", new List<string> { "Store Stock", "Wharehose Stock", "Both", "Alphabetical" }, Main.properties.sortType, value =>
            {
                switch (value)
                {
                    case "Store Stock":
                        Main.properties.sortType = 0;
                        break;
                    case "Wharehose Stock":
                        Main.properties.sortType = 1;
                        break;
                    case "Both":
                        Main.properties.sortType = 2;
                        break;
                    case "Alphabetical":
                        Main.properties.sortType = 3;
                        break;
                }
            });
        }

        private void ConfigureDropdown(string elementName, List<string> choices, int initialValue, Action<string> onChange)
        {
            DropdownField dropdown = rootDocument.Q<DropdownField>(elementName);
            dropdown.choices = choices;
            dropdown.value = choices[initialValue];
            dropdown.RegisterCallback<ChangeEvent<string>>(evt => onChange(evt.newValue));
        }

        private void ConfigureInputsTab()
        {
            ConfigureButton("SortKey", Main.properties.sortKey, false);
            ConfigureButton("MenuKey", Main.properties.settingKey, true);
        }

        private void ConfigureButton(string elementName, string initialValue, bool isMenuOrSort)
        {
            Button button = rootDocument.Q<Button>(elementName);
            button.text = initialValue;
            button.clicked += () => StartCoroutine(AssignKey(button, isMenuOrSort));
        }

        private IEnumerator AssignKey(Button button, bool isMenuOrSort)
        {
            button.text = "Press any key...";
            yield return new WaitUntil(() => Input.anyKeyDown);

            foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>())
            {
                if (Input.GetKeyDown(keyCode))
                {
                    if (isMenuOrSort)
                        Main.properties.settingKey = keyCode.ToString();
                    else
                        Main.properties.sortKey = keyCode.ToString();

                    button.text = keyCode.ToString();
                    break;
                }
            }
        }

        public void ChangeToolBar(int newIndex)
        {
            foreach (VisualElement panel in rootDocument.Q<VisualElement>("TB_Content").Children())
            {
                panel.style.display = DisplayStyle.None;
            }

            rootDocument.Q<VisualElement>("TB_Content").ElementAt(newIndex).style.display = DisplayStyle.Flex;
        }
    }
}
