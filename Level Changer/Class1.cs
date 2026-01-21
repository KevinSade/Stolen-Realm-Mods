using BepInEx;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

[BepInPlugin(
    "com.yourname.shopreset",
    "Shop Reset Hotkey",
    "1.1.0"
)]
public class ShopResetHotkey : BaseUnityPlugin
{
    private static FieldInfo _refreshDictField;
    private static PropertyInfo _currentShopkeeperProp;

    private void Awake()
    {
        Type shopManagerType = typeof(ShopManager);

        _refreshDictField = shopManagerType.GetField(
            "shopkeepNeedsRefreshDict",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        _currentShopkeeperProp = shopManagerType.GetProperty(
            "currentShopkeeper",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (_refreshDictField == null || _currentShopkeeperProp == null)
        {
            Logger.LogError("Failed to bind ShopManager private members");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            TryResetShop();
        }
    }

    private void TryResetShop()
    {
        ShopManager shop = FindObjectOfType<ShopManager>();
        if (shop == null)
        {
            Logger.LogInfo("No ShopManager found");
            return;
        }

        Shopkeeper shopkeeper =
            _currentShopkeeperProp.GetValue(shop) as Shopkeeper;

        if (shopkeeper == null)
        {
            Logger.LogInfo("No current shopkeeper");
            return;
        }

        var refreshDict =
            _refreshDictField.GetValue(shop) as Dictionary<Shopkeeper, bool>;

        if (refreshDict == null)
        {
            Logger.LogInfo("Refresh dictionary not found");
            return;
        }

        // Force refresh
        refreshDict[shopkeeper] = true;

        // Reopen shop → triggers RefreshItemDictSingle internally
        shop.OpenShop();

        Logger.LogInfo("Shop refreshed via Backspace");
    }
}
