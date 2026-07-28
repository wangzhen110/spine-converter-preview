Unicode true
Name "Spine 一键转换预览工具"
!ifndef RELEASE_SUFFIX
  !define RELEASE_SUFFIX "-rc1"
!endif
OutFile "..\dist\SpineConverterPreview-SourceAvailable-Setup-win-x64-1.0.0${RELEASE_SUFFIX}.exe"
InstallDir "$PROGRAMFILES64\SpineConverterPreview"
InstallDirRegKey HKLM "Software\SpineConverterPreview" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
VIProductVersion "1.0.0.0"
VIAddVersionKey "ProductName" "Spine 一键转换预览工具"
VIAddVersionKey "CompanyName" "庆云景智信息技术工作室（个体工商户）"
VIAddVersionKey "FileDescription" "Spine 一键转换预览工具安装程序"
VIAddVersionKey "FileVersion" "1.0.0.0"
VIAddVersionKey "ProductVersion" "1.0.0.0"

Page directory
Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

Section "主程序" SEC_MAIN
  SectionIn RO
  SetOutPath "$INSTDIR"
  File /r "..\dist\product-open-source-win-x64\*.*"
  WriteRegStr HKLM "Software\SpineConverterPreview" "InstallDir" "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\Spine 一键转换预览工具"
  CreateShortcut "$SMPROGRAMS\Spine 一键转换预览工具\Spine 一键转换预览工具.lnk" "$INSTDIR\SpineConverterPreview.exe"
  CreateShortcut "$SMPROGRAMS\Spine 一键转换预览工具\卸载.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpineConverterPreview" "DisplayName" "Spine 一键转换预览工具"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpineConverterPreview" "Publisher" "庆云景智信息技术工作室（个体工商户）"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpineConverterPreview" "DisplayVersion" "1.0.0"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpineConverterPreview" "UninstallString" '"$INSTDIR\Uninstall.exe"'
SectionEnd

Section "桌面快捷方式" SEC_DESKTOP
  CreateShortcut "$DESKTOP\Spine 一键转换预览工具.lnk" "$INSTDIR\SpineConverterPreview.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\Spine 一键转换预览工具.lnk"
  RMDir /r "$SMPROGRAMS\Spine 一键转换预览工具"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\SpineConverterPreview"
  DeleteRegKey HKLM "Software\SpineConverterPreview"
SectionEnd
