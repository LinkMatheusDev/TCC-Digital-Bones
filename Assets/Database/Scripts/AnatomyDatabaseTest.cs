
using System;
using UnityEngine;

public class AnatomyDatabaseTest : MonoBehaviour
{
    private void Start()
    {
        try
        {
            AnatomyDatabase.Initialize();

            AnatomyModelRecord testModel = new AnatomyModelRecord
            {
                ModelId = "TEST_DB",
                DisplayName = "Osso de teste",
                Species = "Teste",
                Bone = "Teste",
                Description = "Registro temporário",
                PrefabKey = "teste"
            };

            AnatomyDatabase.InsertOrUpdate(testModel);

            AnatomyModelRecord result =
                AnatomyDatabase.GetModelById("TEST_DB");

            if (result == null)
                throw new Exception("O registro não foi encontrado.");

            Debug.Log("SUCESSO: cadastro e consulta SQLite funcionando!");

            AnatomyDatabase.DeleteModel("TEST_DB");

            Debug.Log("SUCESSO: exclusão SQLite funcionando!");
        }
        catch (Exception e)
        {
            Debug.LogError("ERRO NO TESTE SQLITE: " + e);
        }
        finally
        {
            AnatomyDatabase.Close();
        }
    }
}