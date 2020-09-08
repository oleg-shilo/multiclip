$packageName = 'multiclip'

# stop "multiclip" first so it does not restart the server assuming it crashed
Stop-Process -Name "multiclip"
Stop-Process -Name "multiclip.server"