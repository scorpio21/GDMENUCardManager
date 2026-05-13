
import re
import os
import glob

defined_keys = {
    "StringSdDrive", "StringTempFolder", "StringSearch", "StringGamesList", "StringLocation", "StringSdNumber",
    "StringSdNumberShort", "StringSdNumberTooltip", "StringSize", "StringTitle", "StringSerial", "StringDisc",
    "StringSaveChanges", "StringPreload", "StringPreloadTooltip", "StringSortList", "StringBatchRename",
    "StringAbout", "StringSelectFolder", "StringRefreshDrive", "StringRename", "StringSentenceCase",
    "StringTitleCase", "StringUppercase", "StringLowercase", "StringAutoRename", "StringAutoRenameTitle",
    "StringUsingIP", "StringRenameUsingIP", "StringUsingFolder", "StringUsingFolderComputer",
    "StringRenameUsingFolder", "StringUsingFile", "StringUsingFileBase", "StringRenameUsingFile",
    "StringViewFileInfo", "StringViewFileInfoToolTip", "StringAddGames", "StringAddGamesToolTip",
    "StringRemoveGame", "StringRemoveGameToolTip", "StringBrowseSdTooltip", "StringBrowseSdToolTip",
    "StringResetTempTooltip", "StringMenuType", "StringSearchFilter", "StringSearchTooltip",
    "StringSearchToolTip", "StringFilterTooltip", "StringFilterToolTip", "StringFilterResetTooltip",
    "StringClearFilterToolTip", "StringDiscOptions", "StringDiscImageOptionsButton", "StringDiscOptionsTooltip",
    "StringDiscImageOptionsToolTip", "StringDatTools", "StringDatToolsButton", "StringDatToolsTooltip",
    "StringDatToolsToolTip", "StringSortListTooltip", "StringSortListButton", "StringSortListToolTip",
    "StringAssignFolder", "StringAssignAltFolders", "StringFolder", "StringArt", "StringType", "StringLockCheck",
    "StringLockCheckTooltip", "StringLockCheckToolTip", "StringSaveChangesTooltip", "StringSaveChangesToolTip",
    "StringUndo", "StringUndoTooltip", "StringUndoToolTip", "StringRedo", "StringRedoTooltip",
    "StringRedoToolTip", "StringBatchFolderRename", "StringBatchFolderRenameButton", "StringBatchFolderRenameTooltip",
    "StringBatchFolderRenameToolTip", "StringAssignArtworkTooltip", "StringEditArtworkTooltip", "StringInfo",
    "StringFileInfo", "StringIpBinInfo", "StringGdiShrink", "StringGdiShrinkDesc", "StringApplyToNew",
    "StringApplyToExisting", "StringAlsoShrinkCompressed", "StringUseBlacklist", "StringVgaPatch",
    "StringVgaPatchDesc", "StringRegionPatch", "StringRegionPatchDesc", "StringSave", "StringImportDatEntries",
    "StringImportDatDesc1", "StringImportDatDesc2", "StringImportDatDesc3", "StringChooseDatFolder",
    "StringImportDatDesc5", "StringImportDatDesc6", "StringBeginImport", "StringImportDatDesc8",
    "StringImportDatDesc9", "StringImportMissing", "StringImportAll", "StringSource", "StringNoFolderSelected",
    "StringExportArtwork", "StringExportArtworkDesc1", "StringExportArtworkDesc2", "StringExportArtworkDesc3",
    "StringExportArtworkDesc4", "StringTarget", "StringChooseTargetFolder", "StringBeginExport",
    "StringClearDatEntries", "StringClearDatDesc1", "StringClearDatDesc2", "StringClearDatDesc4", "StringClearDats",
    "StringOverwriteDats", "StringOverwriteDatsDesc1", "StringOverwriteDatsDesc2", "StringOverwriteDatsDesc3",
    "StringOverwriteDatsDesc4", "StringOverwriteDatsDesc5", "StringOverwriteDatsDesc6", "StringOverwrite",
    "StringFolderPath", "StringFolderPathSpace", "StringAddFolderPath", "StringOk", "StringUpdateWizard",
    "StringUpdatePreserveDesc", "StringPreserveDats", "StringPreserveThemes", "StringPreserveCheats",
    "StringPreserveSettingsDesc", "StringNext", "StringInstallNow", "StringFolderHierarchy", "StringUndoLastChange",
    "StringNoArtworkAssigned", "StringArtworkWarning1", "StringArtworkWarning2", "StringBrowseForImage",
    "StringDeleteEntry", "StringSettings", "StringRefreshDriveToolTip", "StringSelectTempFolderToolTip",
    "StringResetTempFolderToolTip", "StringUseGdMenuToolTip", "StringUseOpenMenuToolTip", "StringAboutToolTip",
    "StringGame", "StringOther", "StringUpdateAvailableTitle", "StringUpdateAvailableNewVersion1",
    "StringUpdateAvailableNewVersion2", "StringViewChangelog", "StringUpdateAvailablePrompt", "StringSkipVersion",
    "StringRemindLater", "StringUpdateNow", "StringMetadataScanTitle", "StringMetadataScanDesc1",
    "StringMetadataScanDesc2", "StringMetadataScanBullet1", "StringMetadataScanBullet2", "StringMetadataScanBullet3",
    "StringMetadataScanDesc3", "StringMetadataScanCount", "StringQuit", "StringStartScan",
    "StringSerialTranslationTitle", "StringSerialTranslationMsg1", "StringSerialTranslationMsg2",
    "StringSerialTranslationMsg3", "StringSerialTranslationInstruct", "StringDatErrorInvalidFolder",
    "StringDatErrorInvalidFolderTitle", "StringDatConfirmImport", "StringDatConfirmImportTitle",
    "StringDatImportFailed", "StringDatImportComplete", "StringDatExportFailed", "StringDatExportComplete",
    "StringDatConfirmClear", "StringDatConfirmClearTitle", "StringDatClearFailed", "StringDatClearCompleteMsg",
    "StringDatClearCompleteTitle", "StringDatConfirmOverwrite", "StringDatConfirmOverwriteTitle",
    "StringDatOverwriteFailed", "StringDatOverwriteCompleteMsg", "StringDatOverwriteCompleteTitle",
    "StringDatErrorOccurred", "StringDatExportedMsg", "StringAboutHeaderTitle", "StringAboutHeaderDesc",
    "StringSupportedFormats", "StringImage", "StringCompressed", "StringKeyboardShortcuts", "StringDelete",
    "StringRemoveSelectedItem", "StringEscape", "StringCloseWindow", "StringRenameSelectedItem", "StringUndoRedo",
    "StringManagingGames", "StringAdd", "StringDragAndDropAdd", "StringRemove", "StringDeleteKey", "StringReorder",
    "StringDragAndDrop", "StringSort", "StringSortListButtonText", "StringSingleOrBulkRenaming", "StringManual",
    "StringManualDesc", "StringAuto", "StringAutoDesc", "StringCase", "StringCaseDesc", "StringFolderPathsOpenMenu",
    "StringFolderPathsDesc", "StringArtworkOpenMenu", "StringArtworkDesc", "StringBatchFolderMoveOpenMenu",
    "StringBatchFolderMoveDesc1", "StringBatchFolderMoveDesc2", "StringDatToolsOpenMenu", "StringDatToolsDesc1",
    "StringDatToolsDesc2", "StringDatToolsDesc3", "StringDatToolsDesc4", "StringGdiShrinkLabel",
    "StringGdiShrinkReduceSize", "StringVgaPatchLabel", "StringVgaPatchForce", "StringRegionFreeLabel",
    "StringRegionFreeBoot", "StringFileFolderLockCheck", "StringFileFolderLockDesc", "StringCredits",
    "StringCheckForUpdates", "StringLoading", "StringFile", "StringCompressedFile", "StringCantLoadCompressed",
    "StringVersion", "StringVga", "StringYes", "StringNo", "StringRegion", "StringDetectedAs",
    "StringCouldNotReadIpBin", "StringError", "StringUnableToFindOrReadFile", "StringProgressWindow",
    "StringCheckingLockedFiles", "StringConfirm", "StringFoldersToDelete", "StringMenuNotSelected",
    "StringOpenMenuDatNotFound", "StringReadingIpBin", "StringPatchingExisting", "StringPatching", "StringDone",
    "StringMessage", "StringSaveChangesToDrive", "StringSavingChanges", "StringUpdatingDatFiles",
    "StringPreparingArchive", "StringPreparing", "StringCopyingGames", "StringCopyingShrinking", "StringCopying",
    "StringConvertingToGdi", "StringConvertingToCdi", "StringConvertingToCcd", "StringConvertingShrinking",
    "StringDecompressing", "StringShrinking", "StringShrinkingExisting", "StringLoadingFileInfo",
    "StringScanningDiscImages", "StringCachingMetadata", "StringFirstTimeSetup", "StringPerformingFirstTimeSetup",
    "StringPerformingFirstTimeDatCopying", "StringAppTitle", "StringSettingsFileReadOnly",
    "StringSettingsFileReadOnlyMsg1", "StringSettingsFileReadOnlyMsg2", "StringSettingsFileReadOnlyMsg3",
    "StringGdemuIniSetup", "StringGdemuIniSetupMsg1", "StringGdemuIniSetupMsg2", "StringGdemuIniSetupMsg3",
    "StringGdemuIniSetupMsg4", "StringAuthentic", "StringClone", "StringRetry", "StringContinue",
    "StringDatFilesMissing", "StringDatFilesMissingMsg", "StringCreate", "StringSkip",
    "StringBoxMissingIconExistsTitle", "StringBoxMissingIconExistsMsg", "StringBoxExistsIconMissingTitle",
    "StringBoxExistsIconMissingMsg", "StringSerialsMismatchTitle", "StringSerialsMismatchMsg",
    "StringInsufficientSpace", "StringSpaceNeeded", "StringNewDiscImages", "StringMenuUpdateBuffer",
    "StringMenuDiscImage", "StringMetadataFiles", "StringTotal", "StringSpaceAvailable", "StringSpaceToBeFreed",
    "StringEffectiveAvailable", "StringShortfall", "StringShrinkSpaceNote", "StringCompressedSpaceNote",
    "StringProceedAnyway", "StringInsufficientSpaceTitle", "StringDiskFullTitle", "StringIncompleteFolderRemoved",
    "StringFreeUpSpace", "StringAppWillClose", "StringSaving", "StringScanning", "StringPatchingTitle",
    "StringShrinkingTitle", "StringRenaming"
}

lower_defined_keys = {k.lower(): k for k in defined_keys}

ignored_resources = {
    "ThemeBackgroundBrush", "ThemeForegroundBrush", "ThemeBorderLowBrush", "BrandColor", "BrandBrush",
    "dreamcastLogoDrawingImage", "dreamcastLogoGeometry", "rowmenu"
}

def scan_files():
    xaml_files = glob.glob("K:/GDMENUCardManager/src/GDMENUCardManager/**/*.xaml", recursive=True)
    axaml_files = glob.glob("K:/GDMENUCardManager/src/GDMENUCardManager.AvaloniaUI/**/*.axaml", recursive=True)
    
    all_files = [f for f in xaml_files + axaml_files if "Languages" not in f]
    
    missing_keys = {}
    casing_issues = {}
    hardcoded_strings = {}
    
    dynamic_resource_re = re.compile(r'DynamicResource\s+([a-zA-Z0-9_]+)')
    # Adjusted regex to avoid matching property names like SizeToContent
    hardcoded_re = re.compile(r'\b(Title|Content|Header|Text|ToolTip|Watermark|ToolTip\.Tip)="([^\{].*?)"')
    
    for file_path in all_files:
        with open(file_path, "r", encoding="utf-8") as f:
            lines = f.readlines()
            for line_no, line in enumerate(lines, 1):
                # Find DynamicResource keys
                for match in dynamic_resource_re.finditer(line):
                    key = match.group(1)
                    if key not in defined_keys and key not in ignored_resources:
                        if key.lower() in lower_defined_keys:
                            if key not in casing_issues:
                                casing_issues[key] = (lower_defined_keys[key.lower()], [])
                            casing_issues[key][1].append(f"{file_path}:{line_no}")
                        else:
                            if key not in missing_keys:
                                missing_keys[key] = []
                            missing_keys[key].append(f"{file_path}:{line_no}")
                
                # Find hardcoded strings
                for match in hardcoded_re.finditer(line):
                    attr = match.group(1)
                    value = match.group(2)
                    
                    # Check if it's SizeToContent="Height" etc.
                    # We check the context of the attribute
                    if f'SizeToContent="{value}"' in line:
                        continue
                    
                    # Ignore common placeholders, tech terms, etc.
                    if not value or value in ["", "X", " → ", "  ", "-", "[", "]", "gdMenu", "openMenu", "0GDTEX.PVR", "Game", "Other", "PSX", "English", "Español", "Height", "WidthAndHeight", "True", "False", "Collapsed", "Visible", "Hidden"]:
                        continue
                    # Ignore URLs
                    if value.startswith("http"):
                        continue
                    
                    if file_path not in hardcoded_strings:
                        hardcoded_strings[file_path] = []
                    hardcoded_strings[file_path].append((line_no, attr, value))
                
    return missing_keys, casing_issues, hardcoded_strings

missing, casing, hardcoded = scan_files()

print("--- MISSING LOCALIZATION KEYS (Truly missing) ---")
for key, occurrences in sorted(missing.items()):
    print(f"Key: {key}")
    for occ in occurrences:
        print(f"  {occ}")

print("\n--- CASING ISSUES (Key exists but with different case) ---")
for key, data in sorted(casing.items()):
    correct_key, occurrences = data
    print(f"Used: {key} -> Should be: {correct_key}")
    for occ in occurrences:
        print(f"  {occ}")

print("\n--- HARDCODED STRINGS THAT SHOULD BE LOCALIZED ---")
for file_path, items in sorted(hardcoded.items()):
    print(f"File: {file_path}")
    for line_no, attr, val in items:
        print(f"  Line {line_no}: {attr}=\"{val}\"")
