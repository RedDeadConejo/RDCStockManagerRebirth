using System;
using System.IO;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;

namespace RDCStockManagerRebirth
{
    public static class RDCSaver
    {
        #region Variables

        // Ruta del archivo JSON
        public static readonly string rutaArchivo = Path.Combine(Application.dataPath, $"RDCData/RDCStockManager.2.0.0b.json");
        #endregion

        #region Métodos
        // Método para guardar datos en un archivo JSON
        public static void GuardarDatos<T>(string rutaArchivo, T datos)
        {
            try
            {
                ComprobarYCrearRuta(rutaArchivo); // Asegurarse de que la ruta existe
                string json = JsonConvert.SerializeObject(datos, Formatting.Indented);
                File.WriteAllText(rutaArchivo, json);
            }
            catch (Exception ex)
            {
                // Manejo de excepciones
                MelonLogger.Msg($"Error al guardar datos: {ex.Message}");
            }
        }

        // Método para cargar datos desde un archivo JSON
        public static T CargarDatos<T>(string rutaArchivo)
        {
            try
            {
                ComprobarYCrearRuta(rutaArchivo); // Asegurarse de que la ruta existe
                if (File.Exists(rutaArchivo))
                {
                    string json = File.ReadAllText(rutaArchivo);
                    return JsonConvert.DeserializeObject<T>(json);
                }
                else
                {
                    MelonLogger.Msg("El archivo no existe.");
                    return default(T);
                }
            }
            catch (Exception ex)
            {
                // Manejo de excepciones
                MelonLogger.Msg($"Error al cargar datos: {ex.Message}");
                return default(T);
            }
        }

        // Método para comprobar si la ruta existe y crearla si no existe
        public static void ComprobarYCrearRuta(string rutaArchivo)
        {
            try
            {
                string directorio = Path.GetDirectoryName(rutaArchivo);
                if (!Directory.Exists(directorio))
                {
                    Directory.CreateDirectory(directorio);
                }
            }
            catch (Exception ex)
            {
                // Manejo de excepciones
                MelonLogger.Msg($"Error al comprobar o crear la ruta: {ex.Message}");
            }
        }
        #endregion
    }

    public class Properties
    {
        [JsonProperty("Enable Boxes Mode")]
        public bool changeType = false;

        [JsonProperty("Boxes Mode: Show number of real boxes and not relative")]
        public bool realBoxesMode = false;

        [JsonProperty("Color For Out of Stock (HEX Format: #ee5253)")]
        public string colorForOutofStock = "#ee5253";

        [JsonProperty("Color For Warming (HEX Format: #ff9f43)")]
        public string colorForWarming = "#ff9f43";

        [JsonProperty("Color Max Item Capacity (HEX Format: #CCC6C6)")]
        public string colorForMaxStock = "#CCC6C6";

        [JsonProperty("Minimum percentage of items in the Store to mark as Out Of Stock (0 - 100)")]
        public float minPercentageDisplay = 20;

        [JsonProperty("Minimum percentage of items in the Store to mark Limited Stock (0 - 100)")]
        public float maxPercentageDisplay = 50;

        [JsonProperty("Minimum percentage of items in the warehouse to mark as Out Of Stock (0 - 100)")]
        public float minItemStorage = 33;

        [JsonProperty("Minimum percentage of items in the warehouse to mark Limited Stock (0 - 100)")]
        public float maxItemStorage = 50;

        [JsonProperty("Change Sort Order: 0 = Ascending | 1 = Descending | 2 = Default")]
        public int sortOrder = 0;

        [JsonProperty("Change Sort Order: 0 = Sort By Store Items | 1 = Sort By Storage Items | 2 = Both | 3 = Alphabetical")]
        public int sortType = 0;

        [JsonProperty("Enable Max Items Capacity")]
        public bool showMaxCapacity = false;

        [JsonProperty("Enable Out Of Stock Notifications")]
        public bool enableNotifications = true;

        [JsonProperty("Sort Item Key")]
        public string sortKey = "T";

        [JsonProperty("Open Setting Key")]
        public string settingKey = "Q";
    }
}
