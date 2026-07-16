namespace LoipvRemote.Protocols.Putty.Monitoring;

public static class LinuxResourceProbe
{
    // The command is fixed application code. No connection field is interpolated into it.
    // It emits invariant key/value pairs so parsing is independent of the remote locale.
    public const string Command = @"LC_ALL=C sh -c '
cpu=""$(awk ""NR==1 {idle=\$5+\$6; total=0; for(i=2;i<=NF;i++) total+=\$i; print total, idle}"" /proc/stat)""
set -- $cpu
cpu_total=$1
cpu_idle=$2
mem_total=""$(awk ""/^MemTotal:/ {print \$2*1024}"" /proc/meminfo)""
mem_available=""$(awk ""/^MemAvailable:/ {print \$2*1024; found=1} END {if(!found) print 0}"" /proc/meminfo)""
disk=""$(df -Pk / | awk ""NR==2 {print \$2*1024, \$3*1024}"" )""
set -- $disk
disk_total=$1
disk_used=$2
network=""$(awk ""NR>2 {iface=\$1; sub(/:$/, x, iface); if(iface !~ /^lo$/) {rx += \$2; tx += \$10}} END {print rx+0, tx+0}"" /proc/net/dev)""
set -- $network
net_rx=$1
net_tx=$2
uptime_seconds=""$(awk ""{print int(\$1)}"" /proc/uptime)""
printf ""cpu_total=%s\n"" ""$cpu_total""
printf ""cpu_idle=%s\n"" ""$cpu_idle""
printf ""mem_total=%s\n"" ""$mem_total""
printf ""mem_available=%s\n"" ""$mem_available""
printf ""disk_total=%s\n"" ""$disk_total""
printf ""disk_used=%s\n"" ""$disk_used""
printf ""net_rx=%s\n"" ""$net_rx""
printf ""net_tx=%s\n"" ""$net_tx""
printf ""uptime_seconds=%s\n"" ""$uptime_seconds""
'";
}
