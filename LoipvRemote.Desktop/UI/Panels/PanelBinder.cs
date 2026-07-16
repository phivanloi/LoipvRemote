using System;
using System.Runtime.Versioning;
using LoipvRemote.App;
using LoipvRemote.App.Composition;
using LoipvRemote.Properties;
using WeifenLuo.WinFormsUI.Docking;
using LoipvRemote.UI;
using System.Windows.Forms;
using System.ComponentModel;

namespace LoipvRemote.UI.Panels
{
    /// <summary>
    /// Manages the binding between Connections and Config panels so they show/hide together when in auto-hide state
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class PanelBinder : IDisposable
    {
        private readonly DesktopWindowCatalog _windows;
        private bool _isProcessing; // Prevent recursive calls

        // Store original auto-hide states
        private DockState _treeFormAutoHideState = DockState.Unknown;
        private DockState _configFormAutoHideState = DockState.Unknown;

        // Store original docked states
        private DockState _treeFormDockedState = DockState.Unknown;
        private DockState _configFormDockedState = DockState.Unknown;

        // Track if panels are temporarily pinned
        private bool _panelsTemporarilyPinned;

        // Timer to check for focus loss
        private System.Windows.Forms.Timer _focusCheckTimer;
        private bool _disposed;

        public PanelBinder(DesktopWindowCatalog windows)
        {
            _windows = windows ?? throw new ArgumentNullException(nameof(windows));
            _focusCheckTimer = new System.Windows.Forms.Timer();
            _focusCheckTimer.Interval = 250; // Check every 250ms
            _focusCheckTimer.Tick += FocusCheckTimer_Tick;

            // Listen for binding option changes
            OptionsTabsPanelsPage.Default.PropertyChanged += OptionsPropertyChanged;
        }

        /// <summary>
        /// Responds to changes in the binding option
        /// </summary>
        private void OptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(OptionsTabsPanelsPage.Default.BindConnectionsAndConfigPanels))
            {
                bool bindingEnabled = OptionsTabsPanelsPage.Default.BindConnectionsAndConfigPanels;
                UpdatePanelBindingState(bindingEnabled);
            }
        }

        /// <summary>
        /// Updates panel states based on binding setting
        /// </summary>
        private void UpdatePanelBindingState(bool bindingEnabled)
        {
            if (_isProcessing)
                return;

            _isProcessing = true;
            try
            {
                if (bindingEnabled)
                {
                    // Binding was enabled - set both panels to auto-hide mode
                    SetPanelsToAutoHide();
                }
                else
                {
                    // Binding was disabled - restore panels to docked mode
                    SetPanelsToDocked();

                    // Stop any active timers
                    _focusCheckTimer.Stop();
                    _panelsTemporarilyPinned = false;
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Sets both panels to auto-hide mode
        /// </summary>
        private void SetPanelsToAutoHide()
        {
            // Save current states if they're not auto-hide
            if (_windows.TreeForm != null && !IsAutoHideState(_windows.TreeForm.DockState))
            {
                _treeFormDockedState = _windows.TreeForm.DockState;

                // Set to auto-hide equivalent
                if (_windows.TreeForm.DockState == DockState.DockLeft)
                    _windows.TreeForm.DockState = DockState.DockLeftAutoHide;
                else if (_windows.TreeForm.DockState == DockState.DockRight)
                    _windows.TreeForm.DockState = DockState.DockRightAutoHide;
                else if (_windows.TreeForm.DockState == DockState.DockTop)
                    _windows.TreeForm.DockState = DockState.DockTopAutoHide;
                else if (_windows.TreeForm.DockState == DockState.DockBottom)
                    _windows.TreeForm.DockState = DockState.DockBottomAutoHide;

                // Save this auto-hide state
                _treeFormAutoHideState = _windows.TreeForm.DockState;
            }

            if (_windows.ConfigForm != null && !IsAutoHideState(_windows.ConfigForm.DockState))
            {
                _configFormDockedState = _windows.ConfigForm.DockState;

                // Set to auto-hide equivalent
                if (_windows.ConfigForm.DockState == DockState.DockLeft)
                    _windows.ConfigForm.DockState = DockState.DockLeftAutoHide;
                else if (_windows.ConfigForm.DockState == DockState.DockRight)
                    _windows.ConfigForm.DockState = DockState.DockRightAutoHide;
                else if (_windows.ConfigForm.DockState == DockState.DockTop)
                    _windows.ConfigForm.DockState = DockState.DockTopAutoHide;
                else if (_windows.ConfigForm.DockState == DockState.DockBottom)
                    _windows.ConfigForm.DockState = DockState.DockBottomAutoHide;

                // Save this auto-hide state
                _configFormAutoHideState = _windows.ConfigForm.DockState;
            }
        }

        /// <summary>
        /// Sets both panels to docked (pinned) mode
        /// </summary>
        private void SetPanelsToDocked()
        {
            // Restore to docked states if available, otherwise convert from auto-hide
            if (_windows.TreeForm != null)
            {
                if (_treeFormDockedState != DockState.Unknown && _treeFormDockedState != DockState.Hidden)
                {
                    _windows.TreeForm.DockState = _treeFormDockedState;
                }
                else if (IsAutoHideState(_windows.TreeForm.DockState))
                {
                    // Convert auto-hide to regular docked
                    if (_windows.TreeForm.DockState == DockState.DockLeftAutoHide)
                        _windows.TreeForm.DockState = DockState.DockLeft;
                    else if (_windows.TreeForm.DockState == DockState.DockRightAutoHide)
                        _windows.TreeForm.DockState = DockState.DockRight;
                    else if (_windows.TreeForm.DockState == DockState.DockTopAutoHide)
                        _windows.TreeForm.DockState = DockState.DockTop;
                    else if (_windows.TreeForm.DockState == DockState.DockBottomAutoHide)
                        _windows.TreeForm.DockState = DockState.DockBottom;
                }

                // Explicitly ensure it's not in auto-hide state
                if (IsAutoHideState(_windows.TreeForm.DockState))
                {
                    _windows.TreeForm.DockState = DockState.DockLeft;
                }
            }

            if (_windows.ConfigForm != null)
            {
                if (_configFormDockedState != DockState.Unknown && _configFormDockedState != DockState.Hidden)
                {
                    _windows.ConfigForm.DockState = _configFormDockedState;
                }
                else if (IsAutoHideState(_windows.ConfigForm.DockState))
                {
                    // Convert auto-hide to regular docked
                    if (_windows.ConfigForm.DockState == DockState.DockLeftAutoHide)
                        _windows.ConfigForm.DockState = DockState.DockLeft;
                    else if (_windows.ConfigForm.DockState == DockState.DockRightAutoHide)
                        _windows.ConfigForm.DockState = DockState.DockRight;
                    else if (_windows.ConfigForm.DockState == DockState.DockTopAutoHide)
                        _windows.ConfigForm.DockState = DockState.DockTop;
                    else if (_windows.ConfigForm.DockState == DockState.DockBottomAutoHide)
                        _windows.ConfigForm.DockState = DockState.DockBottom;
                }

                // Explicitly ensure it's not in auto-hide state
                if (IsAutoHideState(_windows.ConfigForm.DockState))
                {
                    _windows.ConfigForm.DockState = DockState.DockLeft;
                }
            }

            // Reset our tracking variables
            _treeFormAutoHideState = DockState.Unknown;
            _configFormAutoHideState = DockState.Unknown;
        }

        /// <summary>
        /// Initializes event handlers for the Connections and Config panels
        /// </summary>
        public void Initialize()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_windows.TreeForm != null)
            {
                _windows.TreeForm.VisibleChanged += OnTreeFormVisibleChanged;
                _windows.TreeForm.DockStateChanged += OnTreeFormDockStateChanged;
                _windows.TreeForm.Enter += OnPanelEnter;

                // Store initial dock state if not auto-hide
                if (!IsAutoHideState(_windows.TreeForm.DockState))
                    _treeFormDockedState = _windows.TreeForm.DockState;
            }

            if (_windows.ConfigForm != null)
            {
                _windows.ConfigForm.VisibleChanged += OnConfigFormVisibleChanged;
                _windows.ConfigForm.DockStateChanged += OnConfigFormDockStateChanged;
                _windows.ConfigForm.Enter += OnPanelEnter;

                // Store initial dock state if not auto-hide
                if (!IsAutoHideState(_windows.ConfigForm.DockState))
                    _configFormDockedState = _windows.ConfigForm.DockState;
            }

            // Apply initial binding state based on option
            if (OptionsTabsPanelsPage.Default.BindConnectionsAndConfigPanels)
            {
                UpdatePanelBindingState(true);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            OptionsTabsPanelsPage.Default.PropertyChanged -= OptionsPropertyChanged;
            _windows.TreeForm.VisibleChanged -= OnTreeFormVisibleChanged;
            _windows.TreeForm.DockStateChanged -= OnTreeFormDockStateChanged;
            _windows.TreeForm.Enter -= OnPanelEnter;
            _windows.ConfigForm.VisibleChanged -= OnConfigFormVisibleChanged;
            _windows.ConfigForm.DockStateChanged -= OnConfigFormDockStateChanged;
            _windows.ConfigForm.Enter -= OnPanelEnter;
            _focusCheckTimer.Tick -= FocusCheckTimer_Tick;
            _focusCheckTimer.Stop();
            _focusCheckTimer.Dispose();
            GC.SuppressFinalize(this);
        }

        private void OnTreeFormDockStateChanged(object? sender, EventArgs e)
        {
            // Save auto-hide state if it's an auto-hide state
            if (IsAutoHideState(_windows.TreeForm.DockState))
            {
                _treeFormAutoHideState = _windows.TreeForm.DockState;
            }
            // Save docked state if it's a docked state
            else if (_windows.TreeForm.DockState != DockState.Hidden &&
                     _windows.TreeForm.DockState != DockState.Unknown)
            {
                _treeFormDockedState = _windows.TreeForm.DockState;
            }
        }

        private void OnConfigFormDockStateChanged(object? sender, EventArgs e)
        {
            // Save auto-hide state if it's an auto-hide state
            if (IsAutoHideState(_windows.ConfigForm.DockState))
            {
                _configFormAutoHideState = _windows.ConfigForm.DockState;
            }
            // Save docked state if it's a docked state
            else if (_windows.ConfigForm.DockState != DockState.Hidden &&
                     _windows.ConfigForm.DockState != DockState.Unknown)
            {
                _configFormDockedState = _windows.ConfigForm.DockState;
            }
        }

        private void OnTreeFormVisibleChanged(object? sender, EventArgs e)
        {
            // Only act when binding is enabled and not already processing
            if (!OptionsTabsPanelsPage.Default.BindConnectionsAndConfigPanels || _isProcessing)
                return;

            // If the panel was just made visible and both are in auto-hide mode
            if (_windows.TreeForm.Visible &&
                IsPanelAutoHidden(_windows.TreeForm) &&
                IsPanelAutoHidden(_windows.ConfigForm))
            {
                OnPanelEnter(_windows.TreeForm, EventArgs.Empty);
            }
        }

        private void OnConfigFormVisibleChanged(object? sender, EventArgs e)
        {
            // Only act when binding is enabled and not already processing
            if (!OptionsTabsPanelsPage.Default.BindConnectionsAndConfigPanels || _isProcessing)
                return;

            // If the panel was just made visible and both are in auto-hide mode
            if (_windows.ConfigForm.Visible &&
                IsPanelAutoHidden(_windows.TreeForm) &&
                IsPanelAutoHidden(_windows.ConfigForm))
            {
                OnPanelEnter(_windows.ConfigForm, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Handles when a panel is entered (gets focus)
        /// </summary>
        private void OnPanelEnter(object? sender, EventArgs e)
        {
            if (!OptionsTabsPanelsPage.Default.BindConnectionsAndConfigPanels || _isProcessing)
                return;

            // Check if both panels are in auto-hide mode
            if (!IsPanelAutoHidden(_windows.TreeForm) || !IsPanelAutoHidden(_windows.ConfigForm))
                return;

            _isProcessing = true;
            try
            {
                // Store current auto-hide states if not already stored
                if (_treeFormAutoHideState == DockState.Unknown)
                    _treeFormAutoHideState = _windows.TreeForm.DockState;

                if (_configFormAutoHideState == DockState.Unknown)
                    _configFormAutoHideState = _windows.ConfigForm.DockState;

                // Pin both panels temporarily (make them normal docked)
                TemporarilyPinPanels();

                // Start checking for focus loss
                _focusCheckTimer.Start();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// Timer to check if both panels have lost focus
        /// </summary>
        private void FocusCheckTimer_Tick(object? sender, EventArgs e)
        {
            if (!_panelsTemporarilyPinned || _isProcessing)
                return;

            // Get active form in the application
            Form? activeForm = Form.ActiveForm;

            // Check if neither panel has focus
            bool treeHasFocus = _windows.TreeForm != null &&
                               (activeForm == _windows.TreeForm ||
                                _windows.TreeForm.ContainsFocus);

            bool configHasFocus = _windows.ConfigForm != null &&
                                 (activeForm == _windows.ConfigForm ||
                                  _windows.ConfigForm.ContainsFocus);

            // If neither panel has focus and panels are temporarily pinned, restore auto-hide
            if (!treeHasFocus && !configHasFocus)
            {
                _isProcessing = true;
                try
                {
                    RestoreAutoHideState();
                }
                finally
                {
                    _isProcessing = false;
                }
            }
        }

        /// <summary>
        /// Temporarily pins both panels (makes them normal docked panels)
        /// </summary>
        private void TemporarilyPinPanels()
        {
            if (_panelsTemporarilyPinned)
                return;

            // For TreeForm: change from auto-hide to normal docked
            if (_windows.TreeForm != null && IsPanelAutoHidden(_windows.TreeForm))
            {
                // Convert auto-hide state to regular docked state
                if (_windows.TreeForm.DockState == DockState.DockLeftAutoHide)
                    _windows.TreeForm.DockState = DockState.DockLeft;
                else if (_windows.TreeForm.DockState == DockState.DockRightAutoHide)
                    _windows.TreeForm.DockState = DockState.DockRight;
                else if (_windows.TreeForm.DockState == DockState.DockTopAutoHide)
                    _windows.TreeForm.DockState = DockState.DockTop;
                else if (_windows.TreeForm.DockState == DockState.DockBottomAutoHide)
                    _windows.TreeForm.DockState = DockState.DockBottom;
            }

            // For ConfigForm: change from auto-hide to normal docked
            if (_windows.ConfigForm != null && IsPanelAutoHidden(_windows.ConfigForm))
            {
                // Convert auto-hide state to regular docked state
                if (_windows.ConfigForm.DockState == DockState.DockLeftAutoHide)
                    _windows.ConfigForm.DockState = DockState.DockLeft;
                else if (_windows.ConfigForm.DockState == DockState.DockRightAutoHide)
                    _windows.ConfigForm.DockState = DockState.DockRight;
                else if (_windows.ConfigForm.DockState == DockState.DockTopAutoHide)
                    _windows.ConfigForm.DockState = DockState.DockTop;
                else if (_windows.ConfigForm.DockState == DockState.DockBottomAutoHide)
                    _windows.ConfigForm.DockState = DockState.DockBottom;
            }

            _panelsTemporarilyPinned = true;

            // Ensure both panels are visible and active
            if (_windows.TreeForm != null)
            {
                _windows.TreeForm.Show();
                _windows.TreeForm.Activate();
            }

            if (_windows.ConfigForm != null)
            {
                _windows.ConfigForm.Show();
                _windows.ConfigForm.Activate();
            }
        }

        /// <summary>
        /// Restore both panels to their original auto-hide state
        /// </summary>
        private void RestoreAutoHideState()
        {
            if (!_panelsTemporarilyPinned)
                return;

            _focusCheckTimer.Stop();

            // Restore TreeForm to its auto-hide state
            if (_windows.TreeForm != null && _treeFormAutoHideState != DockState.Unknown)
            {
                _windows.TreeForm.DockState = _treeFormAutoHideState;
            }

            // Restore ConfigForm to its auto-hide state
            if (_windows.ConfigForm != null && _configFormAutoHideState != DockState.Unknown)
            {
                _windows.ConfigForm.DockState = _configFormAutoHideState;
            }

            _panelsTemporarilyPinned = false;
        }

        /// <summary>
        /// Checks if a dock state is an auto-hide state
        /// </summary>
        private static bool IsAutoHideState(DockState state)
        {
            return state == DockState.DockLeftAutoHide ||
                   state == DockState.DockRightAutoHide ||
                   state == DockState.DockTopAutoHide ||
                   state == DockState.DockBottomAutoHide;
        }

        /// <summary>
        /// Checks if a panel is in auto-hide state
        /// </summary>
        private static bool IsPanelAutoHidden(DockContent panel)
        {
            if (panel == null)
                return false;

            return IsAutoHideState(panel.DockState);
        }
    }
}
