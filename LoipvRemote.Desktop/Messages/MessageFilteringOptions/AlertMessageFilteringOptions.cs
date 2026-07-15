namespace LoipvRemote.Messages.MessageFilteringOptions
{
    /// <summary>
    /// User-facing alerts are reserved for warnings and errors. Informational
    /// records remain available in the log without interrupting the user.
    /// </summary>
    public sealed class AlertMessageFilteringOptions : IMessageTypeFilteringOptions
    {
        public bool AllowDebugMessages { get; set; }
        public bool AllowInfoMessages { get; set; }
        public bool AllowWarningMessages { get; set; } = true;
        public bool AllowErrorMessages { get; set; } = true;
    }
}
