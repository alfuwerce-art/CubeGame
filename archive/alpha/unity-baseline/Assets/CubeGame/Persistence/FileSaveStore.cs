using System;
using System.IO;
using System.Text;
using CubeGame.Core.Persistence;
using UnityEngine;

namespace CubeGame.Persistence
{
    /// <summary>
    /// Файловое хранилище сохранений в штатном каталоге Unity —
    /// <c>Application.persistentDataPath/CubeGame/saves</c>.
    ///
    /// Запись атомарная в пределах файловой системы: сначала во временный файл, затем замена
    /// целевого файла (с резервной копией <c>.bak</c>). Так прерванная запись не оставляет
    /// наполовину записанный слот, который потом не прочитается.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string _directory;

        public FileSaveStore(string directory = null)
        {
            _directory = string.IsNullOrEmpty(directory)
                ? Path.Combine(Application.persistentDataPath, "CubeGame", "saves")
                : directory;
        }

        /// <summary>Каталог, в котором лежат файлы сохранений.</summary>
        public string DirectoryPath => _directory;

        public bool Exists(string key)
        {
            return File.Exists(PathForKey(key));
        }

        public string Read(string key)
        {
            string path = PathForKey(key);
            if (!File.Exists(path))
            {
                return null;
            }

            return File.ReadAllText(path, Encoding.UTF8);
        }

        public void Write(string key, string contents)
        {
            if (contents == null)
            {
                throw new ArgumentNullException(nameof(contents));
            }

            System.IO.Directory.CreateDirectory(_directory);

            string target = PathForKey(key);
            string temp = target + ".tmp";
            string backup = target + ".bak";

            File.WriteAllText(temp, contents, Utf8NoBom);

            if (File.Exists(target))
            {
                File.Replace(temp, target, backup, true);
            }
            else
            {
                File.Move(temp, target);
            }
        }

        public void Delete(string key)
        {
            string path = PathForKey(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            string backup = path + ".bak";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
        }

        private string PathForKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Ключ сохранения не может быть пустым.", nameof(key));
            }

            foreach (char forbidden in Path.GetInvalidFileNameChars())
            {
                if (key.IndexOf(forbidden) >= 0)
                {
                    throw new ArgumentException($"Ключ сохранения содержит недопустимый символ '{forbidden}'.", nameof(key));
                }
            }

            return Path.Combine(_directory, key + ".sav");
        }
    }
}
