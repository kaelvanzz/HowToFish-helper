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
    }

    private void Update()
    {
        try
        {
            Multiplayer.Tick();
            Roulette.Tick();
            Combat.Tick(MenuOpen);
            Features.TickBoatFlight();
            if (Features.ThirdPerson)
                Features.TickThirdPerson();

            if (InputX.GetKeyDown(KeyCode.Insert))
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
            }

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
