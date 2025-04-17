using System.Collections;
using System.Collections.Generic;
using Guru.SDK.Framework.Utils.Database;
using Guru.SDK.Framework.Utils.Log;
using UnityEngine;

public class Launcher : MonoBehaviour
{
    // Start is called before the first frame update
    async void Start()
    {
        DataBaseMock db = new DataBaseMock();
        await db.InitDatabase();

        Log.D("Database initialized successfully.", "Database");
    }

    // Update is called once per frame
    void Update()
    {

    }
}
