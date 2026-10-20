// Compile-time stand-ins for UnityEngine.CoreModule. Only names and shapes matter;
// the real game assemblies are used at runtime.
using System;
namespace UnityEngine
{
    public class Object
    {
        public string name { get { return null; } set { } }
        public static implicit operator bool(Object o) { return o != null; }
        public static void DontDestroyOnLoad(Object o) { }
        public static void Destroy(Object o) { }
    }
    public class Component : Object
    {
        public GameObject gameObject { get { return null; } }
        public Transform transform { get { return null; } }
        public T GetComponent<T>() { return default(T); }
    }
    public class Behaviour : Component { public bool enabled { get { return true; } set { } } }
    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(System.Collections.IEnumerator e) { return null; }
    }
    public class ScriptableObject : Object { }
    public class GameObject : Object { }
    public class Transform : Component { }
    public class Coroutine { }
    public sealed class SerializeField : Attribute { }
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    public sealed class HideInInspector : Attribute { }
    public sealed class CreateAssetMenuAttribute : Attribute { public string fileName; public string menuName; }
    public static class Debug
    {
        public static void Log(object o) { }
        public static void LogWarning(object o) { }
        public static void LogError(object o) { }
    }
    public static class Random
    {
        public static int Range(int a, int b) { return a; }
        public static float Range(float a, float b) { return a; }
    }
    public static class Mathf
    {
        public static int RoundToInt(float f) { return 0; }
        public static int Clamp(int v, int a, int b) { return v; }
        public static int Min(int a, int b) { return a; }
        public static int Max(int a, int b) { return a; }
    }
    public static class Time
    {
        public static float time { get { return 0; } }
        public static float deltaTime { get { return 0; } }
        public static int frameCount { get { return 0; } }
    }
    public struct Vector2 { public float x, y; }
    public struct Vector3 { public float x, y, z; }
    public struct Vector2Int { public int x { get; set; } public int y { get; set; } }
    public struct Vector3Int { public int x { get; set; } public int y { get; set; } public int z { get; set; } }
    public enum KeyCode { None = 0 }
    public static class Input
    {
        public static bool GetKeyDown(KeyCode k) { return false; }
    }
}
