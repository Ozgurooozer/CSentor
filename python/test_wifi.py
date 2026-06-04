import subprocess

result = subprocess.run(['powershell', '-Command', '(Get-NetAdapter | Where-Object { $_.Name -like "*Wi-Fi*" }).Name'],
                       capture_output=True, text=True, encoding='utf-8', errors='ignore')
print('Adapter:', repr(result.stdout.strip()))

result2 = subprocess.run(['powershell', '-Command', 'Get-CimInstance -ClassName MSNdis_80211_ReceivedSignalStrength -Namespace root/wmi | Select-Object -ExpandProperty Ndis80211ReceivedSignalStrength'],
                        capture_output=True, text=True, encoding='utf-8', errors='ignore')
print('Signal WMI:', repr(result2.stdout.strip()))
print('Signal WMI err:', repr(result2.stderr.strip()))

result3 = subprocess.run(['powershell', '-Command', '(netsh wlan show interfaces) -join "`n"'],
                        capture_output=True, text=True, encoding='utf-8', errors='ignore')
print('Netsh output:', result3.stdout[:1000])
