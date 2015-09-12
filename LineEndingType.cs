//----------------------------------------------------------------------------
// <copyright file="LineEndingType.cs"
//      company="GitPad Team">
//      Copyright (C) 2014 GitPad Team. All rights reserved.
// </copyright>
// <author>GitPad Team</author>
// <description>This is the main code for GitPad.</description>
// <version>v1.4.0 2014-09-16</version>
//
// Based on: https://github.com/GitHub/GitPad
//
//----------------------------------------------------------------------------

namespace Gitpad
{
    public enum LineEndingType
    {
        Windows,    /* CR+LF */
        Posix,      /* LF    */
        MacOS9,     /* CR    */
        Unsure,
    }
}
