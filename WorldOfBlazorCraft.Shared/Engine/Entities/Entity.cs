using System;
using System.Collections.Generic;
using WorldOfBlazorCraft.Shared.Types;

namespace WorldOfBlazorCraft.Shared.Engine.Entities
{
    public class Entity
    {
        public int Id { get; set; }
        public string Kind { get; set; } = string.Empty; // player, mob, npc, object
        public string TemplateId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; } = 1;
        public Vec3 Pos { get; set; } = Vec3.Zero;
        public Vec3 PrevPos { get; set; } = Vec3.Zero;
        public double Facing { get; set; }
        public double PrevFacing { get; set; }
        public bool Dead { get; set; }

        private readonly Dictionary<Type, IComponent> _components = new Dictionary<Type, IComponent>();

        public void AddComponent<T>(T component) where T : IComponent
        {
            _components[typeof(T)] = component;
        }

        public T? GetComponent<T>() where T : class, IComponent
        {
            if (_components.TryGetValue(typeof(T), out var component))
            {
                return component as T;
            }
            return null;
        }

        public bool HasComponent<T>() where T : IComponent
        {
            return _components.ContainsKey(typeof(T));
        }

        public void RemoveComponent<T>() where T : IComponent
        {
            _components.Remove(typeof(T));
        }
    }
}
