﻿using System.Collections.Generic;
using System.Diagnostics;
using RVCore.FindFix;
using RVCore.RvDB;

namespace RVCore.FixFile.Util
{
    public static partial class FixFileUtils
    {
        public static void CheckFilesUsedForFix(List<RvFile> lstFixRomTable, List<RvFile> fileProcessQueue, bool checkRename)
        {
            //Check to see if the files used for fix, can now be set to delete
            List<RvFile> parentCheckList = new List<RvFile>();
            foreach (RvFile fixRom in lstFixRomTable)
            {
                if (fixRom.RepStatus != RepStatus.NeededForFix && (!checkRename || fixRom.RepStatus != RepStatus.Rename)) 
                    continue;
                //check NeededForFix files, and Rename files is checkRename==true

                //if this dir is locked in the tree, just set the fixRom back to InToSort or Unknown
                if (FindFixesListCheck.treeType(fixRom) == RvTreeRow.TreeSelect.Locked)
                {
                    fixRom.RepStatus = fixRom.IsInToSort ? RepStatus.InToSort : RepStatus.Unknown;
                    continue;
                }

                // now set the fixRom to delete, as this fixRom has now been moved to its correct location.
                fixRom.RepStatus = RepStatus.Delete;
                ReportError.LogOut("Setting File Status to Delete:");
                ReportError.LogOut(fixRom);

                switch (fixRom.FileType)
                {
                    // if this is a read fixRom (not zipped) and it has just been set to delete status,
                    // then add it to fileProcessQueue to that it is next to be deleted.
                    case FileType.File:
                        if (fixRom.RepStatus == RepStatus.Delete && !fileProcessQueue.Contains(fixRom))
                        {
                            fileProcessQueue.Add(fixRom);
                        }
                        break;
                    case FileType.ZipFile:
                    case FileType.SevenZipFile:
                        // if this is a compressed fixRom and adds its parent to the parentCheckList to see if the parent can now be reprocessed
                        RvFile checkFile = fixRom.Parent;
                        if (!parentCheckList.Contains(checkFile))
                        {
                            parentCheckList.Add(checkFile);
                        }
                        break;
                    default:
                        ReportError.SendAndShow("Unknown repair fixRom type recheck.");
                        break;
                }

            }


            foreach (RvFile checkFile in parentCheckList)
            {
                // if this fixRom is already in the fileProcessQueue then skip
                if (fileProcessQueue.Contains(checkFile))
                {
                    continue;
                }

                // the parent set has Delete status and no NeededForFix or Rename
                // then is can be processed next to remove the delete status files from it, as the deleted files have now
                // been moved to where they should be.
                bool hasDelete = false;
                bool hasNeededForFix = false;
                for (int i = 0; i < checkFile.ChildCount; i++)
                {
                    RvFile f = checkFile.Child(i);

                    if (f.RepStatus == RepStatus.Delete)
                    {
                        hasDelete = true;
                    }
                    else if (f.RepStatus == RepStatus.NeededForFix || f.RepStatus == RepStatus.Rename)
                    {
                        hasNeededForFix = true;
                        break;
                    }
                }

                // if nothing needed deleted or zip still have NeededForFix or Rename then skip it.
                if (!hasDelete || hasNeededForFix)
                    continue;

                // else add the zip file to the reprocess queue to get cleaned up next
                Debug.WriteLine(checkFile.FullName + " adding to process list.");
                fileProcessQueue.Add(checkFile);
            }
        }
    }
}
