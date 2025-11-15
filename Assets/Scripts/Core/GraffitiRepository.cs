using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GraffitiRepository : MonoBehaviour
{
    public static GraffitiRepository I;

    [Tooltip("JSON index filename in persistentDataPath")]
    public string dbFileName = "graffiti_index.json";

    private readonly List<GraffitiData> _items = new List<GraffitiData>();
    private string _dbPath;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        _dbPath = Path.Combine(Application.persistentDataPath, dbFileName);
        LoadIndex();
    }

    void LoadIndex()
    {
        if (!File.Exists(_dbPath)) return;
        try
        {
            var json = File.ReadAllText(_dbPath);
            var wrapper = JsonUtility.FromJson<Wrapper>(json);
            _items.Clear();
            if (wrapper != null && wrapper.items != null) _items.AddRange(wrapper.items);
        }
        catch { /* ignore corrupt */ }
    }

    void SaveIndex()
    {
        var wrapper = new Wrapper { items = _items.ToArray() };
        var json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(_dbPath, json);
    }

    [System.Serializable] private class Wrapper { public GraffitiData[] items; }

    public IReadOnlyList<GraffitiData> All() => _items;

    public GraffitiData Get(string id) => _items.Find(x => x.id == id);

    public void AddOrUpdate(GraffitiData data)
    {
        var i = _items.FindIndex(x => x.id == data.id);
        if (i >= 0) _items[i] = data; else _items.Add(data);
        SaveIndex();
    }

    public void Delete(string id)
    {
        var data = Get(id);
        if (data != null)
        {
            try
            {
                if (!string.IsNullOrEmpty(data.pngPath) && File.Exists(data.pngPath))
                    File.Delete(data.pngPath);
                if (!string.IsNullOrEmpty(data.thumbPath) && File.Exists(data.thumbPath))
                    File.Delete(data.thumbPath);
            }
            catch { /* ignore */ }
            _items.RemoveAll(x => x.id == id);
            SaveIndex();
        }
    }
}
