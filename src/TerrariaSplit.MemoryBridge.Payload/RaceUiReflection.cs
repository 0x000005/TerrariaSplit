using System;
using System.Reflection;

#pragma warning disable CS8600

namespace TerrariaSplit.MemoryBridge.Payload
{
    internal static class RaceUiReflection
    {
        public static bool TrySetPublicInstanceField<T>(
            object target,
            string fieldName,
            T value)
        {
            if (target == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return false;
            }

            try
            {
                FieldInfo field = target.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public);
                object boxedValue = value;
                if (field == null ||
                    field.IsStatic ||
                    field.IsInitOnly ||
                    field.IsLiteral ||
                    field.DeclaringType == null ||
                    !field.DeclaringType.IsInstanceOfType(target) ||
                    (boxedValue != null && !field.FieldType.IsInstanceOfType(boxedValue)))
                {
                    return false;
                }

                field.SetValue(target, boxedValue);
                return true;
            }
            catch (AmbiguousMatchException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (FieldAccessException)
            {
                return false;
            }
            catch (TargetException)
            {
                return false;
            }
        }
    }
}

#pragma warning restore CS8600
