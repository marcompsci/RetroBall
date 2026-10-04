using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Data
{
    /// <summary>
    /// Base for all content ScriptableObjects. Each asset wraps one engine-free
    /// definition from CallerRetroBall.Logic so the same data can be tuned in the
    /// Inspector and unit-tested outside Unity.
    /// </summary>
    public abstract class DefinitionAsset<T> : ScriptableObject where T : class, IHasId
    {
        [SerializeField] private T definition;

        public T Definition => definition;

        /// <summary>Editor/tooling use: replaces the wrapped definition.</summary>
        public void SetDefinition(T value) => definition = value;
    }
}
