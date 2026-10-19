function Get-WorkflowPanelWindow([int]$ProcessId) {
    # Owned WPF windows are nested beneath Pro in UIA, standalone windows are roots.
    $condition=[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ProcessIdProperty,$ProcessId)
    $until=[DateTime]::UtcNow.AddSeconds(5)
    do {
        $found=@{}
        $roots=[Windows.Automation.AutomationElement]::RootElement.FindAll([Windows.Automation.TreeScope]::Children,$condition)
        foreach($root in $roots){
            if($root.Current.Name -like 'ArcGIS Pro MCP*'){$found[$root.Current.NativeWindowHandle]=$root}
            $windows=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.PropertyCondition]::new([Windows.Automation.AutomationElement]::ControlTypeProperty,[Windows.Automation.ControlType]::Window))
            foreach($window in $windows){if($window.Current.ProcessId -eq $ProcessId -and $window.Current.Name -like 'ArcGIS Pro MCP*'){$found[$window.Current.NativeWindowHandle]=$window}}
        }
        if($found.Count -eq 1){return @($found.Values)[0]}
        if($found.Count -gt 1){throw 'Multiple task panels; resolve identity before capture'}
        Start-Sleep -Milliseconds 150
    }while([DateTime]::UtcNow -lt $until)
    throw 'Actual workflow panel not found in root or owned window tree'
}
