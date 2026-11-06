
using System.Collections.Generic;
using System.IO;
using SQLite;
using UnityEngine;

public static class AnatomyDatabase
{
    private static SQLiteConnection connection;

    private static SQLiteConnection Connection
    {
        get
        {
            if (connection == null)
            {
                Initialize();
            }

            return connection;
        }
    }

    public static void Initialize()
    {
        if (connection != null)
            return;

        string databasePath = Path.Combine(
            Application.persistentDataPath,
            "DigitalBones.db3"
        );

        connection = new SQLiteConnection(databasePath);

        connection.CreateTable<AnatomyModelRecord>();

        Debug.Log("Banco Digital Bones inicializado: " + databasePath);
    }

    public static void InsertOrUpdate(AnatomyModelRecord model)
    {
        Connection.InsertOrReplace(model);
    }

    public static List<AnatomyModelRecord> GetAllModels()
    {
        return Connection
            .Table<AnatomyModelRecord>()
            .ToList();
    }

    public static AnatomyModelRecord GetModelById(string modelId)
    {
        return Connection.Find<AnatomyModelRecord>(modelId);
    }

    public static void DeleteModel(string modelId)
    {
        Connection.Delete<AnatomyModelRecord>(modelId);
    }

    public static void Close()
    {
        if (connection != null)
        {
            connection.Close();
            connection = null;
        }
    }
}