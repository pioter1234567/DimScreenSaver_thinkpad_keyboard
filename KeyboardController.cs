using System;
using System.Reflection;

namespace DimScreenSaver
{
    public class KeyboardController
    {
        private readonly object instance;
        private readonly MethodInfo setBacklight;
        private readonly MethodInfo getBacklight;

        public KeyboardController(string dllPath)
        {
            var ass = Assembly.LoadFile(dllPath);
            var type = ass.GetType("Keyboard_Core.KeyboardControl");
            instance = Activator.CreateInstance(type);

            type.GetMethod("Load")?.Invoke(instance, null);
            type.GetMethod("Initialize")?.Invoke(instance, null);

            setBacklight = type.GetMethod("SetKeyboardBackLightStatus");
            getBacklight = type.GetMethod("GetKeyboardBackLightStatus");
        }

        public uint Set(int level)
        {
            return (uint)setBacklight.Invoke(instance, new object[] { level, null });
        }

        public (uint result, int level) Get()
        {
            object[] args = new object[] { 0, null };
            uint result = (uint)getBacklight.Invoke(instance, args);
            int level = (int)args[0];
            return (result, level);
        }
    }
}
