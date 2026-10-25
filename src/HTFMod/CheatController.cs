using System;
using UnityEngine;

namespace HTFMod;

/// <summary>
/// Per-frame driver for every cheat feature. Lives on a DontDestroyOnLoad object
/// created by ModCore. INSERT toggles the menu; OnGUI draws it.
/// </summary>
public class CheatController : MonoBehaviour
{
    public bool MenuOpen;
    public string Toast;
    public float ToastUntil;

    private float _vitalsTimer;
    private float _moveTimer;
    private float _boatTimer;
    private bool _itemsCached;

    private void Start()
    {
        MenuOpen = false;

        // every menu toggle becomes hotkey-capable (bound via right-click popup / Keys tab)
        foreach (var id in FeatureActions.All)
            Hotkeys.RegisterToggle(id);
    }

    private void Update()
    {
        try
        {
            Hotkeys.TickToggles();
            Multiplayer.Tick();
            Roulette.Tick();
            Combat.Tick(MenuOpen);
            Features.TickBoatFlight();
            if (Features.ThirdPerson)
                Features.TickThirdPerson();

            if (InputX.GetKeyDown(Hotkeys.Key(Hotkeys.MenuToggle)))
            {
                MenuOpen = !MenuOpen;
                if (MenuOpen)
                {
                    Multiplayer.TryHandshake();
                    if (!_itemsCached)
                    {
                        ItemDb.Load();
                        _itemsCached = true;
                    }
                }
                else
                {
                    MenuUi.HandleEscape(); // clear listening/popup state with the menu
                    Settings.SaveNow(); // leaving the menu — flush slider changes
                }
            }
            else if (MenuOpen && InputX.GetKeyDown(KeyCode.Escape))
            {
                // Esc layers: cancel listening → close bind popup → close the menu
                if (!MenuUi.HandleEscape())
                {
                    MenuOpen = false;
                    Settings.SaveNow();
                }
            }

            Settings.Tick();

            _vitalsTimer += Time.deltaTime;
            if (_vitalsTimer >= 0.4f)
            {
                _vitalsTimer = 0f;
                Features.TickVitals();
            }

            _moveTimer += Time.deltaTime;
            if (_moveTimer >= 0.1f)
            {
                _moveTimer = 0f;
                Features.TickMovement();
            }

            _boatTimer += Time.deltaTime;
            if (_boatTimer >= 0.25f)
            {
                _boatTimer = 0f;
                Features.TickBoats();
            }
        }
        catch (Exception e)
        {
            Loader.Log("Update exception: " + e);
        }
    }

    private void OnApplicationQuit()
    {
        Settings.SaveNow();
    }

    private void OnGUI()
    {
        try
        {
            MenuUi.Draw(this);
        }
        catch (Exception e)
        {
            Loader.Log("OnGUI exception: " + e);
        }
    }

    public void ShowToast(string msg)
    {
        Toast = msg;
        ToastUntil = Time.time + 2.2f;
    }
}
