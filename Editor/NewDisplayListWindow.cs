using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class NewDisplayListWindow : EditorWindow
{
    private readonly static Type DISPLAY_ELEMENT_TYPE = typeof(IDisplayElement<>);

    private List<DisplayElementType> _displayElementTypes;
    private string _className;
    private DisplayElementType? _selectedDisplayElement;
    private int? _selectedDataElement;

    private bool IsValid
    {
        get
        {
            return !string.IsNullOrEmpty(_className)
                && _selectedDisplayElement.HasValue
                && (_selectedDisplayElement.Value.DataTypes.Count == 1 || _selectedDataElement.HasValue);
        }
    }

    [MenuItem("Assets/Create/Display List Script", priority = 81)]
    public static void CreateDisplayList()
    {
        var window = ScriptableObject.CreateInstance<NewDisplayListWindow>();
        window.titleContent = new GUIContent("Create Display List Script");
        window.ShowUtility();
    }

    private void Awake()
    {

        _displayElementTypes = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
            {
                return type
                    .GetInterfaces()
                    .Where(iface => iface.IsGenericType)
                    .Any(iface => DISPLAY_ELEMENT_TYPE.IsAssignableFrom(iface.GetGenericTypeDefinition()));
            })
            .Select(displayType =>
            {
                var dataTypes = displayType
                    .GetInterfaces()
                    .Where(iface => iface.GetGenericTypeDefinition() == DISPLAY_ELEMENT_TYPE)
                    .Select(iface => iface.GetGenericArguments()[0])
                    .ToList();
                return new DisplayElementType
                {
                    DisplayType = displayType,
                    DataTypes = dataTypes,
                };
            })
            .ToList();

        var displayElements = _displayElementTypes.Select(type => $"{type.DisplayType.FullName} ({string.Join(", ", type.DataTypes.Select(dataType => dataType.FullName))})");
        Debug.Log($"Found {_displayElementTypes.Count} display element types:\n{string.Join("\n", displayElements)}");
    }

    private void OnGUI()
    {
        _className = EditorGUILayout.TextField("Class Name", _className);

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.PrefixLabel("Display Element");

        var dropdownText = _selectedDisplayElement?.ToString() ?? "Select Display Element...";
        if (EditorGUILayout.DropdownButton(new GUIContent(dropdownText), FocusType.Keyboard))
        {
            var menu = new GenericMenu();

            foreach (var displayElement in _displayElementTypes)
            {
                var selectedElement = displayElement;
                menu.AddItem(
                    new GUIContent(displayElement.ToString()),
                    _selectedDisplayElement.Equals(displayElement),
                    () =>
                    {
                        _selectedDisplayElement = selectedElement;
                    });
            }

            menu.DropDown(GUILayoutUtility.GetLastRect());
        }

        EditorGUILayout.EndHorizontal();

        // Disable the "Create" button until the user has entered valid data for all
        // the fields.
        GUI.enabled = IsValid;

        if (GUILayout.Button("Create"))
        {
            var displayElement = _selectedDisplayElement.Value;
            var dataType = displayElement.DataTypes[_selectedDataElement ?? 0];

            var outputPath = Path.Combine(FindSelectedDirectory(), $"{_className}.cs");

            var scriptAsset = new string[] {
                $"public class {_className} : DisplayList<{displayElement.DisplayType.FullName}, {dataType.FullName}>",
                "{",
                "}",
            };

            File.WriteAllLines(outputPath, scriptAsset);
            AssetDatabase.Refresh();

            Close();
        }
    }

    private static string FindSelectedDirectory()
    {
        var path = "Assets";
        foreach (var obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
        {
            path = AssetDatabase.GetAssetPath(obj);
            if (File.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }
            break;
        }

        return path;
    }
}
