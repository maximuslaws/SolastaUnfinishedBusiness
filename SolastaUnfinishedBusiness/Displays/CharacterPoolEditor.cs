using System;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;
using SolastaUnfinishedBusiness.Api.ModKit;
using SolastaUnfinishedBusiness.Api.Helpers;

namespace SolastaUnfinishedBusiness.Displays
{
    internal sealed class CharacterPoolEditor : IMenuSelectablePage
    {
        public string Name => "Character Pool Editor";

        public int Priority => 800; // position among pages

        private (string path, bool exists, CharacterFileEntry[] files) _scan;
        private bool _scanned;
        private Vector2 _scrollPosition = new Vector2();
        private static string _selectedFileName;
        private CharacterPoolHelper.CharacterFileMetadata _selectedMeta;
        private byte[] _selectedHexBytes = System.Array.Empty<byte>();
        private byte[] _selectedTextBytes = System.Array.Empty<byte>();
        private string _selectedHexString = string.Empty;
        private string _selectedTextPreview = string.Empty;
        private bool _apiScanned;
        private string _apiScanResult = string.Empty;
        private Vector2 _apiScanScroll = new Vector2();
        private string _apiCopyStatus = string.Empty;
        private string _poolDiagResult = string.Empty;
        private bool _poolDiagPresent;
        private Vector2 _poolDiagScroll = new Vector2();
        private string _poolCopyStatus = string.Empty;
        private string _loadedSnapshotResult = string.Empty;
        private object _loadedHero;
        private object _loadedSnapshot;
        private Vector2 _loadedSnapshotScroll = new Vector2();
        private string _loadedCopyStatus = string.Empty;
        private string _compareResult = string.Empty;
        private bool _comparePresent;
        private Vector2 _compareScroll = new Vector2();
        private string _compareCopyStatus = string.Empty;

        private void Refresh()
        {
            _scan = CharacterPoolHelper.GetCharacterFiles();
            _scanned = true;
            // clear selection on refresh
            _selectedFileName = null;
            _selectedMeta = null;
            _selectedHexBytes = System.Array.Empty<byte>();
            _selectedTextBytes = System.Array.Empty<byte>();
            _selectedHexString = string.Empty;
            _selectedTextPreview = string.Empty;
        }

        private void SelectFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            var files = _scan.files ?? System.Array.Empty<CharacterFileEntry>();
            if (!files.Any(f => f.FileName.Equals(fileName, System.StringComparison.CurrentCultureIgnoreCase)))
            {
                return;
            }

            _selectedFileName = fileName;
            _selectedMeta = CharacterPoolHelper.GetCharacterFileMetadata(fileName);
            _selectedHexBytes = CharacterPoolHelper.ReadCharacterFilePrefix(fileName, 256);
            _selectedTextBytes = CharacterPoolHelper.ReadCharacterFilePrefix(fileName, 512);
            _selectedHexString = CharacterPoolHelper.BytesToHexString(_selectedHexBytes);
            _selectedTextPreview = CharacterPoolHelper.BytesToSafeText(_selectedTextBytes);
        }

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            UI.Label();

            using (UI.HorizontalScope())
            {
                if (UI.ActionButton("Refresh", Refresh, UI.Width(100f)))
                {
                    // action handled by Refresh
                }

                GUILayout.Space(10f);
                UI.Label("Character Pool (read-only)");
            }

            if (!_scanned)
            {
                Refresh();
            }

            var path = _scan.path;
            var exists = _scan.exists;
            var files = _scan.files ?? System.Array.Empty<CharacterFileEntry>();

            UI.Label($"Folder: {path}");
            UI.Label($"Exists: {(exists ? "Yes" : "No")}");
            UI.Label($"Found .chr files: {files.Length}");

            UI.Label();

            if (files.Length == 0)
            {
                UI.Label("No character files found.");
                return;
            }

            // header
            using (UI.HorizontalScope())
            {
                UI.Label("Filename", UI.Width(300f));
                UI.Label("Size (KB)", UI.Width(100f));
                UI.Label("Last Modified", UI.Width(220f));
            }

            // scrollable list
            using (var scope = UI.ScrollViewScope(_scrollPosition, UI.Height(300f)))
            {
                _scrollPosition = scope.scrollPosition;

                foreach (var entry in files)
                {
                    using (UI.HorizontalScope())
                    {
                        if (UI.ActionButton(entry.FileName, () => SelectFile(entry.FileName), UI.Width(300f)))
                        {
                            // selected via action
                        }

                        var kb = entry.SizeBytes / 1024.0;
                        UI.Label(kb.ToString("N1"), UI.Width(100f));
                        UI.Label(entry.LastWriteTime == DateTime.MinValue
                            ? ""
                            : entry.LastWriteTime.ToString(), UI.Width(220f));
                    }
                }
            }

            UI.Label();

            // selection details
            if (!string.IsNullOrEmpty(_selectedFileName))
            {
                UI.Label($"Selected: {_selectedFileName}");

                if (_selectedMeta != null)
                {
                    UI.Label($"Full path: {_selectedMeta.FullPath}");
                    UI.Label($"Size: {_selectedMeta.SizeBytes} bytes ({_selectedMeta.SizeKB:N2} KB)");
                    UI.Label($"Created: {_selectedMeta.Created}");
                    UI.Label($"Last modified: {_selectedMeta.LastModified}");
                }

                UI.Label();

                // hex preview (scrollable)
                UI.Label("Hex (first 256 bytes):");
                using (var hexScope = UI.ScrollViewScope(new Vector2(), UI.Height(160f)))
                {
                    GUILayout.Label(_selectedHexString);
                }

                UI.Label();

                // text preview (scrollable)
                UI.Label("Text preview (first 512 bytes):");
                using (var txtScope = UI.ScrollViewScope(new Vector2(), UI.Height(160f)))
                {
                    GUILayout.Label(_selectedTextPreview);
                }
            }

            UI.Label();
            UI.Label("Runtime API Discovery (read-only)");

            using (UI.HorizontalScope())
            {
                if (UI.ActionButton("Scan Character Pool APIs", () =>
                {
                    _apiScanResult = CharacterPoolHelper.DiscoverRuntimeCharacterPoolApis();
                    _apiScanned = true;
                }, UI.Width(220f)))
                {
                }

                GUILayout.Space(10f);

                if (UI.ActionButton("Copy API Scan To Clipboard", () =>
                {
                    GUIUtility.systemCopyBuffer = string.IsNullOrEmpty(_apiScanResult)
                        ? "No API scan result yet."
                        : _apiScanResult;
                    _apiCopyStatus = "API scan copied to clipboard.";
                }, UI.Width(220f)))
                {
                }

                GUILayout.Space(10f);
                UI.Label("(inspects loaded assemblies for relevant types)");
            }

            if (_apiScanned)
            {
                using (var apiScope = UI.ScrollViewScope(_apiScanScroll, UI.Height(300f)))
                {
                    _apiScanScroll = apiScope.scrollPosition;
                    GUILayout.Label(_apiScanResult);
                }
            }

            UI.Label();
            UI.Label("Loaded Snapshot Inspection (read-only)");

            using (UI.HorizontalScope())
            {
                if (UI.ActionButton("Load Snapshot Read-Only", () =>
                {
                    if (string.IsNullOrEmpty(_selectedFileName))
                    {
                        _loadedSnapshotResult = "No file selected.";
                        return;
                    }

                    try
                    {
                        var (hero, snapshot, summary) = CharacterPoolHelper.LoadCharacterSnapshotReadOnly(_selectedFileName);
                        _loadedHero = hero;
                        _loadedSnapshot = snapshot;
                        _loadedSnapshotResult = summary;
                    }
                    catch (System.Exception e)
                    {
                        _loadedSnapshotResult = "Error: " + e.Message;
                    }
                }, UI.Width(220f)))
                {
                }

                GUILayout.Space(10f);

                if (UI.ActionButton("Clear Loaded Snapshot", () =>
                {
                    _loadedSnapshotResult = string.Empty;
                    _loadedHero = null;
                    _loadedSnapshot = null;
                }, UI.Width(220f)))
                {
                }

                GUILayout.Space(10f);
                if (UI.ActionButton("Copy Loaded Snapshot Summary To Clipboard", () =>
                {
                    GUIUtility.systemCopyBuffer = string.IsNullOrEmpty(_loadedSnapshotResult) ? "No loaded snapshot summary." : _loadedSnapshotResult;
                    _loadedCopyStatus = "Loaded snapshot summary copied to clipboard.";
                }, UI.Width(280f)))
                {
                }

                GUILayout.Space(10f);
                if (UI.ActionButton("Compare Loaded Character Structures Read-Only", () =>
                {
                    try
                    {
                        var files = _scan.files ?? System.Array.Empty<CharacterFileEntry>();
                        var names = files.Select(f => f.FileName).ToArray();
                        _compareResult = CharacterPoolHelper.CompareCharactersReadOnly(names, 10);
                        _comparePresent = true;
                    }
                    catch (System.Exception e)
                    {
                        _compareResult = "Error: " + e.Message;
                        _comparePresent = true;
                    }
                }, UI.Width(320f)))
                {
                }
            }

            if (!string.IsNullOrEmpty(_loadedSnapshotResult))
            {
                using (var scope = UI.ScrollViewScope(_loadedSnapshotScroll, UI.Height(300f)))
                {
                    _loadedSnapshotScroll = scope.scrollPosition;
                    GUILayout.Label(_loadedSnapshotResult);
                }
                // small presence labels to avoid unused-field warnings
                UI.Label($"Loaded hero present: {(_loadedHero == null ? "no" : "yes")}");
                UI.Label($"Loaded snapshot present: {(_loadedSnapshot == null ? "no" : "yes")}");
                if (!string.IsNullOrEmpty(_loadedCopyStatus)) UI.Label(_loadedCopyStatus);
            }

            if (_comparePresent && !string.IsNullOrEmpty(_compareResult))
            {
                using (var scope = UI.ScrollViewScope(_compareScroll, UI.Height(400f)))
                {
                    _compareScroll = scope.scrollPosition;
                    GUILayout.Label(_compareResult);
                }
                using (UI.HorizontalScope())
                {
                    if (UI.ActionButton("Copy Comparison To Clipboard", () =>
                    {
                        GUIUtility.systemCopyBuffer = _compareResult;
                        _compareCopyStatus = "Comparison copied to clipboard.";
                    }, UI.Width(260f))) { }
                    GUILayout.Space(10f);
                    if (!string.IsNullOrEmpty(_compareCopyStatus)) UI.Label(_compareCopyStatus);
                }
            }

            UI.Label();
            UI.Label("Character Pool Keys (diagnostic, read-only)");

            using (UI.HorizontalScope())
            {
                if (UI.ActionButton("Inspect Character Pool Keys", () =>
                {
                    if (string.IsNullOrEmpty(_selectedFileName))
                    {
                        _poolDiagResult = "No file selected.";
                        _poolDiagPresent = true;
                        return;
                    }

                    try
                    {
                        _poolDiagResult = CharacterPoolHelper.InspectCharacterPoolKeys(_selectedFileName);
                        _poolDiagPresent = true;
                    }
                    catch (System.Exception e)
                    {
                        _poolDiagResult = "Error: " + e.Message;
                        _poolDiagPresent = true;
                    }
                }, UI.Width(220f)))
                {
                }

                GUILayout.Space(10f);

                if (UI.ActionButton("Copy Pool Diagnostic To Clipboard", () =>
                {
                    GUIUtility.systemCopyBuffer = string.IsNullOrEmpty(_poolDiagResult) ? "No pool diagnostic." : _poolDiagResult;
                    _poolCopyStatus = "Pool diagnostic copied to clipboard.";
                }, UI.Width(220f)))
                {
                }
            }

            if (_poolDiagPresent && !string.IsNullOrEmpty(_poolDiagResult))
            {
                using (var poolScope = UI.ScrollViewScope(_poolDiagScroll, UI.Height(300f)))
                {
                    _poolDiagScroll = poolScope.scrollPosition;
                    GUILayout.Label(_poolDiagResult);
                }
            }
        }
    }
}
