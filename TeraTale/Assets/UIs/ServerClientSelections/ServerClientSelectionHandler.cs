﻿using UnityEngine;
using System.Diagnostics;
using UnityEngine.SceneManagement;

public class ServerClientSelectionHandler : MonoBehaviour
{
    public Town town;
    public Forest forest;

    void Start()
    {
        Screen.SetResolution(1280, 720, false);
        //OnServer("Boss");
        OnClient();
    }

    public void OnDatabase()
    {
        var database = new Process();
        database.StartInfo.FileName = "C:\\Users\\Lobo\\Desktop\\Projects\\TeraTale\\Database\\Database\\bin\\Debug\\Database.exe";
        database.Start();
    }

    public void OnLogin()
    {
        var login = new Process();
        login.StartInfo.FileName = "C:\\Users\\Lobo\\Desktop\\Projects\\TeraTale\\Login\\Login\\bin\\Debug\\Login.exe";
        login.Start();
    }

    public void OnServer(string name)
    {
        GameObject.Find(name).GetComponent<GameServer>().enabled = true;
        SceneManager.LoadScene(name);
    }

    public void OnProxy()
    {
        var proxy = new Process();
        proxy.StartInfo.FileName = "C:\\Users\\Lobo\\Desktop\\Projects\\TeraTale\\Proxy\\Proxy\\bin\\Debug\\Proxy.exe";
        proxy.Start();
    }

    public void OnClient()
    {
        FindObjectOfType<Certificator>().enabled = true;
        SceneManager.LoadScene("Logo");
    }
}
