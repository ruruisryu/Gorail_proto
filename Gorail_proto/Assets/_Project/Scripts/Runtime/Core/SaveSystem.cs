using System.IO;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 슬롯 1개짜리 JSON 세이브. Application.persistentDataPath/save.json에 저장한다.
    /// </summary>
    public static class SaveSystem
    {
        static string Path => System.IO.Path.Combine(Application.persistentDataPath, "save.json");

        public static bool HasSave => File.Exists(Path);

        public static void Save(SaveData data)
        {
            string json = JsonUtility.ToJson(data, prettyPrint: false);
            File.WriteAllText(Path, json);
        }

        public static SaveData Load()
        {
            if (!HasSave) return null;
            string json = File.ReadAllText(Path);
            return JsonUtility.FromJson<SaveData>(json);
        }

        public static void Delete() => File.Delete(Path);
    }
}
