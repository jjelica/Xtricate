﻿using System.Collections.Generic;

namespace Xtricate.Configuration
{
    public interface IAppSettings
    {
        Dictionary<string, string> GetAll();

        List<string> GetAllKeys();

        bool Exists(string key);

        void Set<T>(string key, T value);

        string GetString(string name);

        IList<string> GetList(string key);

        IDictionary<string, string> GetDictionary(string key);

        T Get<T>(string name);

        T Get<T>(string name, T defaultValue);
    }
}