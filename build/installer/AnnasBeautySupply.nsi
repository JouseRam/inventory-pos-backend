; Installer of the desktop register: one file that installs the app, its shortcuts with the store's icon and an uninstaller.
; Per-user install (no administrator prompt): %LOCALAPPDATA%\Programs\AnnasBeautySupply. The data (database, backups,
; license) live apart in %LOCALAPPDATA%\MatizPOS and survive reinstalls and uninstalls.
;
; Build:  makensis -DSRC=<publish folder> -DOUT=<setup .exe> build/installer/AnnasBeautySupply.nsi
; (build/installer/build.sh publishes the register and runs this.)

Unicode true
SetCompressor /SOLID lzma

!ifndef SRC
  !error "Pasa la carpeta publicada con -DSRC=<carpeta>"
!endif
!ifndef OUT
  !define OUT "Instalar-AnnasBeautySupply.exe"
!endif
!ifndef VERSION
  !define VERSION "1.0.0"
!endif

!define APPNAME "Anna’s Beauty Supply POS"
!define EXE "MatizPOS.exe"
!define UNINSTKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\AnnasBeautySupplyPOS"
!define WEBVIEW2_GUID "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"

Name "${APPNAME}"
OutFile "${OUT}"
InstallDir "$LOCALAPPDATA\Programs\AnnasBeautySupply"
InstallDirRegKey HKCU "${UNINSTKEY}" "InstallLocation"
RequestExecutionLevel user
BrandingText "Anna’s Beauty Supply"

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APPNAME}"
VIAddVersionKey "CompanyName" "Anna’s Beauty Supply"
VIAddVersionKey "FileDescription" "Instalador de ${APPNAME}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "LegalCopyright" "Anna’s Beauty Supply"

!include "MUI2.nsh"

!define MUI_ICON "..\..\InventoryPOS.Desktop\app.ico"
!define MUI_UNICON "..\..\InventoryPOS.Desktop\app.ico"
!define MUI_WELCOMEFINISHPAGE_BITMAP "welcome.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "welcome.bmp"
!define MUI_ABORTWARNING

!define MUI_WELCOMEPAGE_TITLE "Instalar ${APPNAME}"
!define MUI_WELCOMEPAGE_TEXT "Se instalará el punto de venta de Anna’s Beauty Supply en esta computadora, con su acceso directo en el escritorio y en el menú Inicio.$\r$\n$\r$\nLa primera vez que lo abras te pedirá activar la computadora con una clave que te da tu proveedor del sistema.$\r$\n$\r$\nPulsa Siguiente para continuar."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "Listo"
!define MUI_FINISHPAGE_TEXT "${APPNAME} quedó instalado. Ábrelo desde el icono del escritorio."
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Abrir ${APPNAME} ahora"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Spanish"

; The register runs a single instance guarded by this mutex: files in use cannot be replaced.
!macro CheckNotRunning
  retry:
    System::Call 'kernel32::OpenMutexW(i 0x00100000, i 0, w "Local\MatizPOS.Register") p .r0'
    ${If} $0 P<> 0
      System::Call 'kernel32::CloseHandle(p r0)'
      MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "El punto de venta está abierto. Ciérralo y pulsa Reintentar." IDRETRY retry
      Abort
    ${EndIf}
!macroend

Function .onInit
  !insertmacro CheckNotRunning
FunctionEnd

Section "Programa" SecMain
  SetOutPath "$INSTDIR"
  ; A clean copy: files of an earlier version that no longer exist must not stay behind.
  RMDir /r "$INSTDIR\wwwroot"
  File /r "${SRC}\*.*"
  File "/oname=app.ico" "..\..\InventoryPOS.Desktop\app.ico"

  CreateShortCut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\app.ico" 0
  CreateDirectory "$SMPROGRAMS\Anna’s Beauty Supply"
  CreateShortCut "$SMPROGRAMS\Anna’s Beauty Supply\${APPNAME}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\app.ico" 0
  CreateShortCut "$SMPROGRAMS\Anna’s Beauty Supply\Desinstalar.lnk" "$INSTDIR\Desinstalar.exe"

  WriteUninstaller "$INSTDIR\Desinstalar.exe"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayName" "${APPNAME}"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayIcon" "$INSTDIR\app.ico"
  WriteRegStr HKCU "${UNINSTKEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTKEY}" "Publisher" "Anna’s Beauty Supply"
  WriteRegStr HKCU "${UNINSTKEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTKEY}" "UninstallString" '"$INSTDIR\Desinstalar.exe"'
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTKEY}" "NoRepair" 1

  ; The register's window is Microsoft Edge WebView2; Windows 10/11 usually have it. Offer the download when missing.
  ReadRegStr $1 HKLM "SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_GUID}" "pv"
  ${If} $1 == ""
  ${OrIf} $1 == "0.0.0.0"
    ReadRegStr $1 HKCU "Software\Microsoft\EdgeUpdate\Clients\${WEBVIEW2_GUID}" "pv"
  ${EndIf}
  ${If} $1 == ""
  ${OrIf} $1 == "0.0.0.0"
    MessageBox MB_YESNO|MB_ICONINFORMATION "Falta un componente de Windows que usa el punto de venta (Microsoft Edge WebView2).$\r$\n$\r$\n¿Abrir la página de descarga? Instálalo y luego abre el punto de venta." IDNO +2
      ExecShell "open" "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
  ${EndIf}
SectionEnd

Function un.onInit
  !insertmacro CheckNotRunning
FunctionEnd

Section "Uninstall"
  Delete "$DESKTOP\${APPNAME}.lnk"
  RMDir /r "$SMPROGRAMS\Anna’s Beauty Supply"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${UNINSTKEY}"
  ; The data stays: database, backups and activation, in case the register is installed again.
  MessageBox MB_OK|MB_ICONINFORMATION "Se quitó el programa. Las ventas, el inventario y los respaldos se conservan en $LOCALAPPDATA\MatizPOS."
SectionEnd
