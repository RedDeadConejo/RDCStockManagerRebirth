using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using HarmonyLib;
using UnityEngine;
using System.IO;
using MyBox;
using System.Reflection;
using TMPro;
using UnityEngine.Localization.Components;

namespace RDCStockManagerRebirth
{
    public class Main : MelonMod
    {
        #region Properties
        private static ProductViewer productViewer;

        public static Properties properties = RDCSaver.CargarDatos<Properties>(RDCSaver.rutaArchivo);


        private static CanvasGroup CheckoutNotification;

        private UIDocument settingGenerator;

        private static Dictionary<int, float> ProductStatus = new Dictionary<int, float>();

        #endregion

        #region Melon Methods

        //Notifica que el mod ha sido cargado
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Stock Manager has started!");
        }

        //Notifica que el mod ha sido descargado
        public override void OnDeinitializeMelon()
        {
            MelonLogger.Msg("Stock Manager has stopped!");
        }

        //Comprueba si esta el mod StoreDelivery cargado, para poner el parche.
        public override void OnLateInitializeMelon()
        {
            if (IsDependencyLoaded("StoreDelivery"))
            {
                MelonLogger.Msg("Applying patch to StoreDelivery....");
                HarmonyInstance.PatchAll(typeof(StoreDeliveryPatcher));
                MelonLogger.Msg("Patch Applied");

            }

            RDCSaver.ComprobarYCrearRuta(RDCSaver.rutaArchivo);


            if (!File.Exists(RDCSaver.rutaArchivo))
            {
                RDCSaver.GuardarDatos<Properties>(RDCSaver.rutaArchivo, new Properties());
            }

        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            //Carga el metodo GameSceneLoaded cuando se carga la escena de juego.
            switch (buildIndex)
            {
                case 1:
                    GameEsceneLoaded();
                    break;
            }
        }

        #endregion

        #region Unity Methods

        public override void OnUpdate()
        {
            //Ordena la lista de articulos si se pulsa la tecla de ordenar.
            if (Input.GetKeyUp(ConvertStringToKey(properties.sortKey)) && productViewer)
            {
                OrderItemList(productViewer);
            }
            
            //Muestra el panel de configuracion si se pulsa la tecla de configuracion.
            if (Input.GetKeyUp(ConvertStringToKey(properties.settingKey)))
            {
                SwitchConfigPanel();
            }
        }

        #endregion

        #region Personalized Methods

        //Metodo para cambiar la visibilidad del panel de configuracion.
        public void SwitchConfigPanel()
        {
            VisualElement settingPanel = settingGenerator.rootVisualElement.Q<VisualElement>("Menu");

            settingPanel.style.display = settingPanel.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
        }

        //Muestra una notificacion de que un producto se esta agotando.
        public static void ShowProductStockAlert()
        {
            CheckoutNotification.OpenAnimation(0.2f, 0.5f, 2f, delegate
            {
                CheckoutNotification.gameObject.SetActive(false);
            }, false);
        }

        public static void UpdateUserSettings()
        {
            properties = RDCSaver.CargarDatos<Properties>(RDCSaver.rutaArchivo);
            UpdateProductList();
        }

        public void GameEsceneLoaded()
        {
            //Busca el componente ProductViewer en la escena.
            productViewer = GameObject.Find("---GAME---/Computer/Screen/Market App/Tabs/Products Tab/Products Screen").GetComponent<ProductViewer>();

            //Carga y crea el boton de Stock Manager en la pantalla.
            if (settingGenerator == null)
            {
                settingGenerator = UnityEngine.Object.Instantiate(LoadAsset<GameObject>("stockmanagerassets.rdc", "StockManager")).GetComponent<UIDocument>();
                settingGenerator.gameObject.AddComponent<StockManagerUI>();
                GameObject.DontDestroyOnLoad(settingGenerator);

                var existingStockManager = GameObject.Find("StockManager(Clone)");
                if (existingStockManager != null)
                {
                    UnityEngine.Object.Destroy(existingStockManager);
                }
            }

            if (CheckoutNotification == null)
            {
                GameObject notificationReference = GameObject.Find("---UI---/Warning Canvas/Interaction Warning");
                CanvasGroup cloneNotifier = GameObject.Instantiate(notificationReference).GetComponent<CanvasGroup>();
                cloneNotifier.transform.SetParent(notificationReference.transform.parent);
                cloneNotifier.gameObject.SetActive(false);
                cloneNotifier.gameObject.name = "StockManager Notification";
                cloneNotifier.transform.localPosition = notificationReference.transform.localPosition;

                var localizeStringEvent = cloneNotifier.transform.GetChild(0).GetChild(0).GetComponent<LocalizeStringEvent>();
                if (localizeStringEvent != null)
                {
                    GameObject.Destroy(localizeStringEvent);
                }

                cloneNotifier.transform.GetChild(0).GetChild(0).GetComponent<TMP_Text>().text = "An product is running out of stock!";
                CheckoutNotification = cloneNotifier;
            }
        }

        //Metodo para calcular la capacidad de productos en la tienda y almacen.
        private static void CalculateProductCapacities(int itemID, out string displayMaxProductCapacity, out string storageMaxProductCapacity, out int boxID)
        {
            displayMaxProductCapacity = "0";
            storageMaxProductCapacity = "0";
            boxID = 0;

            if (!IsItemOnShop(itemID)) return;

            var productSO = Singleton<IDManager>.Instance.ProductSO(itemID);
            displayMaxProductCapacity = (GetUniqueDisplaySlots(itemID, false).Count * productSO.GridLayoutInStorage.productCount).ToString();

            if (!Singleton<SaveManager>.Instance.Storage.Purchased) return;

            foreach (BoxSO boxSO in Singleton<IDManager>.Instance.Boxes)
            {
                if (boxSO.BoxSize == productSO.GridLayoutInBox.boxSize)
                {
                    boxID = boxSO.ID;
                    break;
                }
            }

            int boxCount = Singleton<IDManager>.Instance.BoxSO(boxID).GridLayout.boxCount;
            storageMaxProductCapacity = ((GetRacksSlots(itemID) * boxCount) * productSO.GridLayoutInBox.productCount).ToString();
        }

        //Metodo para calcular la cantidad de un producto en la tienda y almacen.
        private static void CalculateProductAmounts(int itemID, int boxProductsCapacity, out float displayProductAmmountPerBox, out float storageProductAmmountPerBox)
        {
            displayProductAmmountPerBox = (float)DisplayedProductsInShop[itemID] / boxProductsCapacity;
            storageProductAmmountPerBox = (float)DisplayedProductsInStorage(itemID) / boxProductsCapacity;
        }

        //Metodo para calcular la cantidad final de un producto en la tienda y almacen.
        private static void CalculateFinalCounts(int itemID, int boxProductsCapacity, float displayProductAmmountPerBox, float storageProductAmmountPerBox, out string shopProductFinalCountPerBox, out string storageProductFinalCountPerBox)
        {
            shopProductFinalCountPerBox = DisplayedProductsInShop[itemID] % boxProductsCapacity == 0 ? displayProductAmmountPerBox.ToString("n0") : displayProductAmmountPerBox.ToString("n1");
            storageProductFinalCountPerBox = DisplayedProductsInStorage(itemID) % boxProductsCapacity == 0 ? storageProductAmmountPerBox.ToString("n0") : storageProductAmmountPerBox.ToString("n1");
        }

        //Metodo para actualizar los valores de un producto en la tienda.
        public static void UpdatesValues(SalesItem saleUI)
        {
            if (productViewer == null)
            {
                MelonLogger.Msg("Stock Manager: No se encontra un productViewer! (UpdateValues())");
                return;
            }

            int itemID = GetSaleUIElementID(saleUI);
            TMP_Text itemText = GetSaleUITextMesh(saleUI);

            if (!InventoryManager.Instance.IsProductDisplayed(itemID))
            {
                return;
            }

            CalculateProductCapacities(itemID, out string displayMaxProductCapacity, out string storageMaxProductCapacity, out int boxID);

            int boxProductsCapacity = Singleton<IDManager>.Instance.ProductSO(itemID).ProductAmountOnPurchase;
            CalculateProductAmounts(itemID, boxProductsCapacity, out float displayProductAmmountPerBox, out float storageProductAmmountPerBox);

            CalculateFinalCounts(itemID, boxProductsCapacity, displayProductAmmountPerBox, storageProductAmmountPerBox, out string shopProductFinalCountPerBox, out string storageProductFinalCountPerBox);

            float totalItemCapacity = (displayProductAmmountPerBox / (int.Parse(displayMaxProductCapacity) / boxProductsCapacity)) * 100;
            float totalItemStorageCapacity = 0f;

            if (MaxProductCapacityOnStorage.ContainsKey(itemID))
            {
                totalItemStorageCapacity = (storageProductAmmountPerBox / (GetRacksSlots(itemID) * Singleton<IDManager>.Instance.BoxSO(boxID).GridLayout.boxCount)) * 100;
            }

            string displaySlotCount = DisplayedProductsInShop[itemID].ToString();
            string storageSlotCount = DisplayedProductsInStorage(itemID).ToString();

            if (properties.changeType)
            {
                displaySlotCount = shopProductFinalCountPerBox;

                float storeCapacity = float.Parse(displayMaxProductCapacity);
                float storageCapacity = float.Parse(storageMaxProductCapacity);

                displayMaxProductCapacity = (storeCapacity / Singleton<IDManager>.Instance.ProductSO(itemID).GridLayoutInBox.productCount).ToString();
                storageMaxProductCapacity = (storageCapacity / Singleton<IDManager>.Instance.ProductSO(itemID).GridLayoutInBox.productCount).ToString();

                storageSlotCount = properties.realBoxesMode ? DisplayedBoxesInStorage(itemID).ToString() : storageProductFinalCountPerBox;
            }

            //Controla las notificaciones de stock.
            if (properties.enableNotifications)
                if (totalItemCapacity <= properties.minPercentageDisplay)
                    ShowProductStockAlert();

            itemText.richText = true;

            itemText.text = FormatProductText(itemID, totalItemCapacity, totalItemStorageCapacity, displaySlotCount, storageSlotCount, Singleton<SaveManager>.Instance.Storage.Purchased, displayMaxProductCapacity, storageMaxProductCapacity);
        }

        public static void OrderItemList(ProductViewer productViewer)
        {
            var listaDesordenada = ProductStatus.ToList();

            if (properties.sortType == 3)
            {
                listaDesordenada.Sort((x, y) => GetSaleUIElement(x.Key).ProductName.CompareTo(GetSaleUIElement(y.Key).ProductName));
            }
            else
            {
                listaDesordenada.Sort((x, y) => x.Value.CompareTo(y.Value));
            }

            if (properties.sortOrder == 1)
            {
                listaDesordenada.Reverse();
            }

            for (int i = 0; i < listaDesordenada.Count; i++)
            {
                GetSaleUIElement(listaDesordenada[i].Key).transform.SetSiblingIndex(i);
            }
        }

        public static void UpdateProductList()
        {
            if (productViewer != null)
            {
                Type productViewerType = typeof(ProductViewer);
                FieldInfo salesItemsField = productViewerType.GetField("m_SalesItems", BindingFlags.NonPublic | BindingFlags.Instance);
                List<SalesItem> salesItemsList = salesItemsField.GetValue(productViewer) as List<SalesItem>;

                if (salesItemsList != null)
                {
                    for (int i = 0; i < salesItemsList.Count; i++)
                    {
                        if (salesItemsList[i].GetType().ToString() != "Furniture Sales Item")
                        {
                            UpdatesValues(salesItemsList[i]);
                        }
                    }
                }
            }
            else
            {
                SetProductViewer(GameObject.FindObjectOfType<ProductViewer>());
                MelonLogger.Msg("Stock Manager: No se encontró un productViewer! (UpdateProductList())");
            }
        }
        
        #endregion

        #region Text Methods

        private static string FormatProductText(int productId, float totalDisplayCapacity, float totalStorageCapacity, string displaySlotCount, string storageSlotCount, bool isStoragePurchased, string maxDisplayCapacity, string maxStorageCapacity)
        {
            string shopColor = GetColor(totalDisplayCapacity, properties.minPercentageDisplay, properties.maxPercentageDisplay);
            string storageColor = GetColor(totalStorageCapacity, properties.minItemStorage, properties.maxItemStorage);
            string nameColor = "#" + ColorUtility.ToHtmlStringRGBA(Color.white);

            #region Temp Text Name Color Changer
            if (totalDisplayCapacity <= properties.minPercentageDisplay || totalStorageCapacity <= properties.minItemStorage && isStoragePurchased)
            {
                nameColor = properties.colorForOutofStock;
            }
            else if (totalStorageCapacity > properties.minItemStorage && totalStorageCapacity < properties.maxItemStorage && isStoragePurchased
                || totalDisplayCapacity > properties.minPercentageDisplay && totalDisplayCapacity < properties.maxPercentageDisplay)
            {

                nameColor = properties.colorForWarming;
            }
            #endregion

            UpdateProductStatus(productId, displaySlotCount, storageSlotCount);

            if (isStoragePurchased)
            {
                if (properties.showMaxCapacity)
                    return FormatProductTextWithMaxCapacity(productId, shopColor, storageColor, nameColor, displaySlotCount, storageSlotCount, maxDisplayCapacity, maxStorageCapacity);

                return FormatProductTextWithoutMaxCapacity(productId, shopColor, storageColor, nameColor, displaySlotCount, storageSlotCount);
            }

            if (properties.showMaxCapacity)
                return FormatProductTextWithMaxCapacity(productId, shopColor, nameColor, displaySlotCount, maxDisplayCapacity);

            return FormatProductTextWithoutMaxCapacity(productId, shopColor, nameColor, displaySlotCount);
        }

        private static string GetColor(float capacity, float minCapacity, float maxCapacity)
        {
            if (capacity <= minCapacity)
                return properties.colorForOutofStock;
            if (capacity > minCapacity && capacity < maxCapacity)   
                return properties.colorForWarming;
            return "#" + ColorUtility.ToHtmlStringRGBA(Color.white);
        }

        private static void UpdateProductStatus(int productId, string displaySlotCount, string storageSlotCount)
        {
            switch (properties.sortType)
            {
                case 0:
                    ProductStatus[productId] = displaySlotCount.TryParseToFloat().RoundToInt();
                    break;
                case 1:
                    ProductStatus[productId] = storageSlotCount.TryParseToFloat().RoundToInt();
                    break;
                case 2:
                    ProductStatus[productId] = displaySlotCount.TryParseToFloat().RoundToInt() + storageSlotCount.TryParseToFloat().RoundToInt();
                    break;
            }
        }

        private static string FormatProductTextWithMaxCapacity(int productId, string shopColor, string storageColor, string nameColor, string displaySlotCount, string storageSlotCount, string maxDisplayCapacity, string maxStorageCapacity)
        {
            return $"<color={nameColor}>{Singleton<IDManager>.Instance.ProductSO(productId).TempProductName} <color={shopColor}>({displaySlotCount}/<color={properties.colorForMaxStock}>{maxDisplayCapacity}<color={shopColor}>) <color={storageColor}>({storageSlotCount}/<color={properties.colorForMaxStock}>{maxStorageCapacity}<color={storageColor}>)";
        }

        private static string FormatProductTextWithMaxCapacity(int productId, string shopColor, string nameColor, string displaySlotCount, string maxDisplayCapacity)
        {
            return $"<color={nameColor}>{Singleton<IDManager>.Instance.ProductSO(productId).TempProductName} <color={shopColor}>({displaySlotCount}/<color={properties.colorForMaxStock}>{maxDisplayCapacity}<color={shopColor}>)";
        }

        private static string FormatProductTextWithoutMaxCapacity(int productId, string shopColor, string storageColor, string nameColor, string displaySlotCount, string storageSlotCount)
        {
            return $"<color={nameColor}>{Singleton<IDManager>.Instance.ProductSO(productId).TempProductName} <color={shopColor}>({displaySlotCount}) <color={storageColor}>({storageSlotCount})";
        }

        private static string FormatProductTextWithoutMaxCapacity(int productId, string shopColor, string nameColor, string displaySlotCount)
        {
            return $"<color={nameColor}>{Singleton<IDManager>.Instance.ProductSO(productId).TempProductName} <color={shopColor}>({displaySlotCount})";
        }


        #endregion

        #region Data Access Methods

        //Metodo para obtener un diccionario con los productos desbloqueados.
        private static Dictionary<int, int> DisplayedProductsInShop
        {
            get
            {
                Type type = typeof(InventoryManager);
                FieldInfo privateVar = type.GetField("m_DisplayedProducts", BindingFlags.NonPublic | BindingFlags.Instance);
                return privateVar.GetValue(InventoryManager.Instance) as Dictionary<int, int>;
            }
        }

        //Metodo para optener la cantidad de un producto en el almacen.
        private static int DisplayedProductsInStorage(int itemID)
        {
            if (!Singleton<SaveManager>.Instance.Storage.Purchased)
            {
                return 0;
            }

            Type rackManagerType = typeof(RackManager);
            Type rackSlotType = typeof(RackSlot);
            FieldInfo rackSlotsField = rackManagerType.GetField("m_RackSlots", BindingFlags.NonPublic | BindingFlags.Instance);
            Dictionary<int, List<RackSlot>> racks = rackSlotsField.GetValue(RackManager.Instance) as Dictionary<int, List<RackSlot>>;

            if (racks == null || !racks.TryGetValue(itemID, out List<RackSlot> slots))
            {
                return 0;
            }

            int totalProductCount = 0;
            foreach (RackSlot slot in slots)
            {
                totalProductCount += slot.Data.TotalProductCount;
            }

            return totalProductCount;
        }

        //Metodo para obtener la cantidad de cajas de un producto en el almacen.
        private static int DisplayedBoxesInStorage(int itemID)
        {
            if (!Singleton<SaveManager>.Instance.Storage.Purchased)
            {
                return 0;
            }

            Type rackManagerType = typeof(RackManager);
            FieldInfo rackSlotsField = rackManagerType.GetField("m_RackSlots", BindingFlags.NonPublic | BindingFlags.Instance);
            Dictionary<int, List<RackSlot>> racks = rackSlotsField.GetValue(RackManager.Instance) as Dictionary<int, List<RackSlot>>;

            if (racks == null || !racks.TryGetValue(itemID, out List<RackSlot> slots))
            {
                return 0;
            }

            int totalBoxCount = 0;
            foreach (RackSlot slot in slots)
            {
                FieldInfo m_Boxes = typeof(RackSlot).GetField("m_Boxes", BindingFlags.NonPublic | BindingFlags.Instance);
                List<Box> boxes = m_Boxes.GetValue(slot) as List<Box>;
                totalBoxCount += boxes?.Count ?? 0;
            }

            return totalBoxCount;
        }

        //Metodo para optener una diccionario con la capacidad de cada producto en el almacen.
        private static Dictionary<int, int> MaxProductCapacityOnStorage
        {
            get
            {
                var rackManagerInstance = Singleton<RackManager>.Instance;
                var field = typeof(RackManager).GetField("m_Racks", BindingFlags.Instance | BindingFlags.NonPublic);
                var racks = field.GetValue(rackManagerInstance) as List<Rack>;

                var productCapacity = new Dictionary<int, int>();

                foreach (var rack in racks)
                {
                    foreach (var rackSlot in rack.RackSlots)
                    {
                        int productId = rackSlot.Data.ProductID;
                        if (productCapacity.ContainsKey(productId))
                        {
                            productCapacity[productId]++;
                        }
                        else
                        {
                            productCapacity[productId] = 1;
                        }
                    }
                }

                return productCapacity;
            }
        }


        #endregion

        #region Utility Methods

        //Cambia el valor de productViewer.
        public static void SetProductViewer(ProductViewer _productViewer)
        {
            productViewer = _productViewer;
        }

        //Metodo para optener el ID de un producto en el ordenador.
        private static int GetSaleUIElementID(SalesItem saleUI)
        {
            if (saleUI == null)
            {
                throw new ArgumentNullException(nameof(saleUI), "El parámetro saleUI no puede ser nulo.");
            }

            Type typeSalesUIElement = typeof(SalesItem);
            FieldInfo mProductNameText = typeSalesUIElement.GetField("m_ProductID", BindingFlags.NonPublic | BindingFlags.Instance);

            if (mProductNameText == null)
            {
                throw new InvalidOperationException("No se pudo encontrar el campo 'm_ProductID' en la clase SalesItem.");
            }

            return (int)mProductNameText.GetValue(saleUI);
        }

        //Metodo para comprobar si un producto esta en la tienda.
        private static bool IsItemOnShop(int itemID)
        {
            var m_DisplayedProducts = typeof(DisplayManager)
                .GetField("m_DisplayedProducts", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(DisplayManager.Instance) as Dictionary<int, List<DisplaySlot>>;

            return m_DisplayedProducts?.ContainsKey(itemID) ?? false;
        }

        //Metodo para obtener el texto de un producto en el ordenador.
        private static TMP_Text GetSaleUITextMesh(SalesItem saleUI)
        {
            Type typeSalesUIElement = typeof(SalesItem);
            FieldInfo mProductNameText = typeSalesUIElement.GetField("m_ProductNameText", BindingFlags.NonPublic | BindingFlags.Instance);
            return mProductNameText.GetValue(saleUI) as TMP_Text;
        }

        //Metodo para optener la cantidad de espacios en el almacen de un producto.
        private static int GetRacksSlots(int productID)
        {
            foreach (KeyValuePair<int, int> keyValuePair in MaxProductCapacityOnStorage)
            {
                if (keyValuePair.Key == productID)
                {
                    return keyValuePair.Value;
                }
            }
            return 0;
        }

        //Metodo para obtener un producto en el ordenador.
        public static SalesItem GetSaleUIElement(int itemID)
        {
            try
            {
                if (productViewer != null)
                {
                    var m_SalesItems = typeof(ProductViewer)
                        .GetField("m_SalesItems", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.GetValue(productViewer) as List<SalesItem>;

                    return m_SalesItems?.FirstOrDefault(item => GetSaleUIElementID(item) == itemID);
                }
            }
            catch (Exception ex)
            {
                //Debug.Log($"StockManager: {ex}");
            }
            return null;
        }

        //Metodo para convertir un string a KeyCode
        public KeyCode ConvertStringToKey(string _Key)
        {
            if (System.Enum.TryParse(_Key, true, out KeyCode keyCode))
            {
                return keyCode;
            }
            return KeyCode.None;
        }

        //Metodo para comprobar si una dependencia esta cargada
        private bool IsDependencyLoaded(string dependencyName)
        {
            // Verifica si la dependencia está en la lista de mods cargados
            foreach (var mod in MelonMod.RegisteredMelons)
            {
                if (mod.Info.Name == dependencyName)
                {
                    MelonLogger.Msg($"Se encontro la dependencia: {dependencyName}.");
                    return true;
                }
            }
            MelonLogger.Msg($"No se encontro la dependencia: {dependencyName}.");
            return false;
        }

        //Metodo para cargar un assetbundle
        public T LoadAsset<T>(string assetBundleName, string objectNameToLoad) where T : UnityEngine.Object
        {
            // Cargar el AssetBundle (esto es solo un ejemplo, asegúrate de que tu lógica de carga es correcta)
            AssetBundle assetBundle = AssetBundle.LoadFromFile(Path.Combine(MelonUtils.UserDataDirectory, "RDCStockManager", assetBundleName));
            if (assetBundle == null)
            {
                MelonLogger.Error("Failed to load AssetBundle!");
                return null;
            }

            // Cargar el objeto desde el AssetBundle
            UnityEngine.Object loadedObject = assetBundle.LoadAsset(objectNameToLoad, typeof(T));
            if (loadedObject == null)
            {
                MelonLogger.Error("Failed to load object from AssetBundle!");
                return null;
            }

            // Instanciar el objeto y devolverlo como tipo T
            T instantiatedObject = GameObject.Instantiate(loadedObject) as T;

            // Descargar el AssetBundle
            assetBundle.Unload(false);

            return instantiatedObject;
        }

        //Metodo para obtener la informacion de una caja mediente una interaccion con la caja.
        public static BoxData GetBoxDataFormInteraction(BoxInteraction boxInteraction)
        {
            if (boxInteraction == null)
            {
                throw new ArgumentNullException(nameof(boxInteraction), "El parámetro boxInteraction no puede ser nulo.");
            }

            Type boxInteractionType = typeof(BoxInteraction);
            FieldInfo boxField = boxInteractionType.GetField("m_Box", BindingFlags.NonPublic | BindingFlags.Instance);

            if (boxField == null)
            {
                throw new InvalidOperationException("No se pudo encontrar el campo 'm_Box' en la clase BoxInteraction.");
            }

            Box box = boxField.GetValue(boxInteraction) as Box;

            return box?.Data;
        }

        //Metodo para obetener una lista de DisplaySlot unicos.
        public static List<DisplaySlot> GetUniqueDisplaySlots(int itemID, bool hasProduct)
        {
            var displaySlots = new HashSet<DisplaySlot>(DisplayManager.Instance.GetDisplaySlots(itemID, hasProduct));
            return displaySlots.ToList();
        }

        #endregion

        #region Harmony Patcher

        //Estable el productoviewer que se usara para coger los valores
        [HarmonyPatch(typeof(ProductViewer), "Start")]
        public static class ProductViewerStartPatch
        {
            public static void Prefix(ProductViewer __instance)
            {
                Main.SetProductViewer(__instance);
            }
        }

        #region UpdatesValues

        [HarmonyPatch(typeof(BoxInteraction), "PlaceBoxToRack")]
        public static class PlaceBoxToRackPatch
        {
            private static BoxData boxData;

            public static void Prefix(BoxInteraction __instance)
            {
                try
                {
                    boxData = Main.GetBoxDataFormInteraction(__instance);
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"Stock Manager: {ex}");
                }
            }

            public static void Postfix(BoxInteraction __instance)
            {
                try
                {
                    if (boxData == null)
                    {
                        MelonLogger.Msg("Stock Manager: No se ha podido encontrar datos en la caja!");
                        return;
                    }

                    var saleUIElement = Main.GetSaleUIElement(boxData.ProductID);
                    if (saleUIElement != null)
                    {
                        Main.UpdatesValues(saleUIElement);
                    }
                    else
                    {
                        MelonLogger.Msg("Stock Manager: No se ha podido coger el SaleUIElement de la caja!");
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Msg($"Stock Manager: {e.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(BoxInteraction), "TakeProductFromDisplay")]
        public static class SetCurrentRackSlotPatch
        {
            public static void Postfix(BoxInteraction __instance)
            {
                try
                {
                    var boxData = Main.GetBoxDataFormInteraction(__instance);
                    if (boxData != null && boxData.ProductID > 0)
                    {
                        var saleUIElement = Main.GetSaleUIElement(boxData.ProductID);
                        if (saleUIElement != null)
                        {
                            Main.UpdatesValues(saleUIElement);
                        }
                        else
                        {
                            //MelonLogger.Msg("Stock Manager: No se ha podido coger el SaleUIElement de la caja!");
                        }
                    }
                    else
                    {
                        //MelonLogger.Msg("Stock Manager: No se ha podido encontrar datos en la caja!");
                    }
                }
                catch (Exception e)
                {
                    //MelonLogger.Msg("Stock Manager: " + e.ToString());
                }
            }
        }

        [HarmonyPatch(typeof(RackSlot), "TakeBoxFromRack")]
        public static class OnTakePatch
        {
            public static void Postfix(Box __result)
            {
                try
                {
                    if (__result == null)
                    {
                        MelonLogger.Msg("Stock Manager: No se ha podido encontrar datos en la caja!");
                        return;
                    }

                    var saleUIElement = Main.GetSaleUIElement(__result.Data.ProductID);
                    if (saleUIElement != null)
                    {
                        Main.UpdatesValues(saleUIElement);
                    }
                    else
                    {
                        MelonLogger.Msg("Stock Manager: No se ha podido coger el SaleUIElement de la caja!");
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Msg("Stock Manager: " + e.ToString());
                }
            }
        }

        [HarmonyPatch(typeof(InventoryManager), "AddProductToDisplay")]
        public static class PlaceProductsPatch
        {
            public static void Postfix(ItemQuantity productData)
            {
                try
                {
                    SalesItem saleItem = Main.GetSaleUIElement(productData.FirstItemID);

                    if (saleItem)
                    {
                        Main.UpdatesValues(saleItem);
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Msg("Stock Manager: " + e.Message);
                }
            }
        }

        [HarmonyPatch(typeof(Customer), "TakeProduct")]
        public static class TakeProductPatch
        {
            public static void Postfix(DisplaySlot displaySlot, int productID)
            {
                try
                {
                    SalesItem salesItem = Main.GetSaleUIElement(productID);
                    if (salesItem)
                    {
                        Main.UpdatesValues(salesItem);
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Msg(e.Message);
                }
            }
        }

        #endregion

        #region UpdateProductList Patches

        [HarmonyPatch(typeof(DisplaySlot), "SpawnProduct")]
        public static class SpawnProductDisplaySlotPatch
        {
            public static void Postfix()
            {
                try
                {
                    Main.UpdateProductList();
                }
                catch (Exception e)
                {
                    MelonLogger.Msg("Stock Manager: " + e.Message);
                }
            }
        }

        [HarmonyPatch(typeof(RackSlot), "SetLabel")]
        public static class SetLabelDisplaySlotPatch
        {
            public static void Postfix()
            {
                try
                {
                    Main.UpdateProductList();
                }
                catch (Exception e)
                {
                    MelonLogger.Msg("Stock Manager: " + e.Message);
                }
            }
        }

        [HarmonyPatch(typeof(RackSlot), "TakeBoxFromRack")]
        public static class TakeBoxFromRackSlotPatch
        {
            public static void Postfix()
            {
                try
                {
                    Main.UpdateProductList();
                }
                catch (Exception e)
                {
                    MelonLogger.Msg("Stock Manager: " + e.Message);
                }
            }
        }

        [HarmonyPatch(typeof(Restocker), "MoveTo")]
        public static class MoveToPatch
        {
            public static void Prefix()
            {
                try
                {
                    Main.UpdateProductList();
                }
                catch (Exception e)
                {
                    MelonLogger.Msg("Stock Manager: " + e.Message);
                }
            }
        }

        [HarmonyPatch(typeof(Label), "InstantInteract")]
        public static class DisableTagLabelPatch
        {
            public static void Postfix()
            {
                try
                {
                    Main.UpdateProductList();
                }
                catch (Exception e)
                {
                    MelonLogger.Msg("Stock Manager: " + e.Message);
                }
            }
        }

        [HarmonyPatch(typeof(ProductViewer), "SpawnUnlockedProducts")]
        public static class SpawnUnlockedProductsPatch
        {
            public static void Postfix()
            {
                try
                {
                    Main.UpdateProductList();
                }
                catch (Exception e)
                {
                    MelonLogger.Msg("Stock Manager: " + e.Message);
                }
            }
        }

        #endregion

#endregion

    }
}
