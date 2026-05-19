using System;
using System.Reflection;
using GameDevTV.RTS.Units;
using UnityEngine;

namespace GameDevTV.RTS.TechTree
{
    public abstract class UpgradeSO : UnlockableSO, IModifier
    {
        [field: SerializeField] public string PropertyPath { get; private set; }

        public abstract void Apply(AbstractUnitSO unit);

        protected T GetPropertyValue<T>(AbstractUnitSO unit, out object target, out PropertyInfo propertyInfo)
        {
            // if PropertyPath = "AttackConfig/Damage"...
            string[] attributes = PropertyPath.Split("/"); // ["AttackConfig", "Damage"]

            Type type = unit.GetType();
            target = unit;

            for (int i = 0; i < attributes.Length - 1; i++)
            {
                propertyInfo = type.GetProperty(attributes[i]);

                if (propertyInfo == null)
                {
                    Debug.LogError($"Unable to apply modifier {Name} to attribute {PropertyPath} because" +
                        $" it does not exist on {unit.Name}!");
                    throw new InvalidPathSpecifiedException(attributes[i]);
                }

                target = propertyInfo.GetValue(target); // target is now AttackConfigSO!
                type = target.GetType(); // type is now AttackConfigSO instead of AbstractUnitSO!
            }

            propertyInfo = type.GetProperty(attributes[^1]); // Damage!

            if (propertyInfo == null)
            {
                Debug.LogError($"Unable to apply modifier {Name} to attribute {PropertyPath} because" +
                        $" it does not exist on {unit.Name}!");
                throw new InvalidPathSpecifiedException(attributes[^1]);
            }

            T returnValue = default;
            try
            {
                returnValue = (T)propertyInfo.GetValue(target);
            }
            catch (InvalidCastException)
            {
                Debug.LogError($"Expected {PropertyPath} to be an int, but it wasn't!");
            }

            return returnValue;
        }

        /// <summary>
        /// Mục tiêu: Ghi giá trị vào property trên SO (kể cả <c>private set</c> của auto-property).
        /// Cách hoạt động: Thử SetValue công khai → invoke setter non-public → gán backing field <c>&lt;Name&gt;k__BackingField</c>.
        /// </summary>
        protected void SetPropertyValue(object target, PropertyInfo propertyInfo, object value)
        {
            if (target == null || propertyInfo == null)
            {
                throw new ArgumentNullException(target == null ? nameof(target) : nameof(propertyInfo));
            }

            try
            {
                propertyInfo.SetValue(target, value);
                return;
            }
            catch (ArgumentException)
            {
                // Property chỉ có private set — thử cách khác bên dưới.
            }

            MethodInfo nonPublicSetter = propertyInfo.GetSetMethod(nonPublic: true);
            if (nonPublicSetter != null)
            {
                nonPublicSetter.Invoke(target, new[] { value });
                return;
            }

            Type declaringType = propertyInfo.DeclaringType;
            if (declaringType != null)
            {
                FieldInfo backingField = declaringType.GetField(
                    $"<{propertyInfo.Name}>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                if (backingField != null)
                {
                    backingField.SetValue(target, value);
                    return;
                }
            }

            Debug.LogError(
                $"Unable to apply modifier {Name} to {PropertyPath}: no setter on {target.GetType().Name}.");
            throw new InvalidPathSpecifiedException(propertyInfo.Name);
        }
    }
}