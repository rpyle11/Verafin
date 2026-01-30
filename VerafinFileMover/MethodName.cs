using System.Reflection;

namespace VerafinFileMover
{
    public static class MethodName
    {
        public static string GetMethodName(MethodBase? method)
        {
            if (method?.DeclaringType == null) return string.Empty;
            var methodName = method.DeclaringType.FullName;

            if (methodName != null && (methodName.Contains('>') || methodName.Contains('<')))
                methodName = methodName.Split('<', '>')[1];
            else
                methodName = method.Name;

            return methodName;
        }

    }
}
