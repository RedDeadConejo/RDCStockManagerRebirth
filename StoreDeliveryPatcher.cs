using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using StoreDelivery;

namespace RDCStockManagerRebirth
{
    internal class StoreDeliveryPatcher
    {
        [HarmonyPatch(typeof(RackTool), "PlaceBoxInRack")]
        private static void Postfix(RackSlot rackSlot, Box box)
        {
            Main.UpdatesValues(Main.GetSaleUIElement(box.Product.ID));
        }
    }
}
