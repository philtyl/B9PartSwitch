﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using KSP;

namespace B9PartSwitch
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ConfigField : Attribute
    {
        public bool persistant = false;
        public string configName = null;
    }

    public class ConfigFieldInfo
    {
        public Behaviour Instance { get; private set; }
        public FieldInfo Field { get; private set; }
        public ConfigField Attribute { get; private set; }
        
        public ConstructorInfo Constructor { get; protected set; }

        public ConfigFieldInfo(Behaviour instance, FieldInfo field, ConfigField attribute)
        {
            Instance = instance;
            Field = field;
            Attribute = attribute;

            if (Attribute.configName == null || Attribute.configName == string.Empty)
                Attribute.configName = Field.Name;

            RealType = Type;
        }

        protected void FindConstructor()
        {
            Constructor = null;
            ConstructorInfo[] constructors = RealType.GetConstructors();

            foreach (ConstructorInfo constructor in constructors)
            {
                ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length == 0)
                {
                    Constructor = constructor;
                    return;
                }
            }
        }

        public string Name { get { return Field.Name; } }
        public string ConfigName { get { return Attribute.configName; } }
        public Type Type { get { return Field.FieldType; } }
        private Type realType;
        public Type RealType
        {
            get
            {
                return realType;
            }
            set
            {
                realType = value;
                IsComponentType = RealType.IsSubclassOf(typeof(Component));
                IsRegisteredParseType = CFGUtil.ParseTypeRegistered(RealType);
                IsConfigNodeType = IsRegisteredParseType ? false : RealType.GetInterfaces().Contains(typeof(IConfigNode));
                IsSerializableType = RealType.IsUnitySerializableType();
                if (!IsSerializableType)
                    Debug.LogWarning("The type " + RealType.Name + " is not a Unity serializable type and thus will not be serialized.  This may lead to unexpected behavior, e.g. the field is null after instantiating a prefab.");

                FindConstructor();
            }
        }
        public bool IsComponentType { get; private set; }
        public bool IsRegisteredParseType { get; private set; }
        public virtual bool IsConfigNodeType { get; private set; }
        public bool IsSerializableType { get; private set; }
        public bool IsPersistant { get { return Attribute.persistant; } }
        public object Value
        {
            get
            {
                return Field.GetValue(Instance);
            }
            set
            {
                Field.SetValue(Instance, value);
            }
        }
    }

    public class ListFieldInfo : ConfigFieldInfo
    {
        public IList List { get; private set; }
        public int Count { get { return List.Count; } }

        public ListFieldInfo(Behaviour instance, FieldInfo field, ConfigField attribute)
            : base(instance, field, attribute)
        {
            List = Field.GetValue(Instance) as IList;
            if (List == null)
                throw new ArgumentNullException("Cannot initialize with a null list (or object is not a list)");
            RealType = Type.GetGenericArguments()[0];

            FindConstructor();

            if (IsConfigNodeType && Constructor == null)
            {
                throw new MissingMethodException("A default constructor is required for the IConfigNode type " + RealType.Name + " (constructor required to parse list field " + field.Name + " in class " + Instance.GetType().Name + ")");
            }
        }

        public void ParseNodes(ConfigNode[] nodes)
        {
            if (!IsConfigNodeType)
                throw new NotImplementedException("The generic type of this list (" + RealType.Name + ") is not an IConfigNode");
            if (nodes.Length == 0)
                return;

            List.Clear();
            foreach (ConfigNode node in nodes)
            {
                IConfigNode obj;
                if (RealType.IsSubclassOf(typeof(Component)))
                    obj = Instance.gameObject.AddComponent(RealType) as IConfigNode;
                else
                    obj = Constructor.Invoke(null) as IConfigNode;

                obj.Load(node);
                List.Add(obj);
            }
        }

        public void ParseValues(string[] values)
        {
            if (!IsRegisteredParseType)
                throw new NotImplementedException("The generic type of this list (" + RealType.Name + ") is not a registered parse type");
            if (values.Length == 0)
                return;

            List.Clear();
            foreach (string value in values)
            {
                object obj = CFGUtil.ParseConfigValue(RealType, value);
                List.Add(obj);
            }
        }

        public ConfigNode[] FormatNodes()
        {
            if (!IsConfigNodeType)
                throw new NotImplementedException("The generic type of this list (" + RealType.Name + ") is not an IConfigNode");

            ConfigNode[] nodes = new ConfigNode[Count];

            for (int i = 0; i < Count; i++)
            {
                nodes[i] = new ConfigNode();
                IConfigNode obj = List[i] as IConfigNode;
                obj.Save(nodes[i]);
            }

            return nodes;
        }

        public string[] FormatValues()
        {
            if (IsConfigNodeType)
                throw new NotImplementedException("The generic type of this list (" + RealType.Name + ") is an IConfigNode");
            if (!IsRegisteredParseType)
                throw new NotImplementedException("The generic type of this list (" + RealType.Name + ") is not a registered parse type");

            string[] values = new string[Count];

            for (int i = 0; i < Count; i++)
            {
                values[i] = CFGUtil.FormatConfigValue(List[i]);
            }

            return values;
        }
    }
}
