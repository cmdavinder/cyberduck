/*
 *  Copyright (c) 2004 David Kocher. All rights reserved.
 *  http://cyberduck.ch/
 *
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  Created by August Mueller on Wed Feb 04 2004.
 *  Bug fixes, suggestions and comments should be sent to:
 *  dkocher@cyberduck.ch
 */

/* DO NOT EDIT THIS FILE - it is machine generated */
#include <jni.h>
/* Header for class ch_cyberduck_ui_cocoa_odb_Editor */

#ifndef _Included_ch_cyberduck_ui_cocoa_odb_Editor
#define _Included_ch_cyberduck_ui_cocoa_odb_Editor
#ifdef __cplusplus
extern "C" {
#endif
/* Inaccessible static: log */
/* Inaccessible static: class_00024ch_00024cyberduck_00024ui_00024cocoa_00024odb_00024Editor */
/*
 * Class:     ch_cyberduck_ui_cocoa_odb_Editor
 * Method:    edit
 * Signature: (Ljava/lang/String;)V
 */
JNIEXPORT void JNICALL Java_ch_cyberduck_ui_cocoa_odb_Editor_edit
  (JNIEnv *, jobject, jstring);

#ifdef __cplusplus
}
#endif
#endif

#import <Cocoa/Cocoa.h>
#include "Editor.h"

@interface Editor : NSObject
{
	jclass		editorClass;
	jobject		editorObject;
	JNIEnv*		env;
}

- (id)init:(JNIEnv*)env withEditorClass:(jclass)editorClass withEditorObject:(jobject)editorObject;

- (IBAction) odbEdit:(id) sender path:(NSString *)path;

@end
