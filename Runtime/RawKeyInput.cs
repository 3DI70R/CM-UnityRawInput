﻿using System;
using System.Collections.Generic;
 using UnityEngine;

 namespace UnityRawInput
{
    public static class RawKeyInput
    {
        /// <summary>
        /// Event invoked when user presses a key.
        /// </summary>
        public static event Func<RawKey, bool> OnKeyDown;
        /// <summary>
        /// Event invoked when user releases a key.
        /// </summary>
        public static event Func<RawKey, bool> OnKeyUp;

        /// <summary>
        /// Whether the service is running and input messages are being processed.
        /// </summary>
        public static bool IsRunning => hookPtr != IntPtr.Zero;
        /// <summary>
        /// Whether any key is currently pressed.
        /// </summary>
        public static bool AnyKeyDown => pressedKeys.Count > 0;
        
        /// <summary>
        /// Number of pressed keys at the moment
        /// </summary>
        public static int PressedKeysCount => pressedKeys.Count;
        
        /// <summary>
        /// Whether input messages should be handled when the application is not in focus.
        /// </summary>
        public static bool WorkInBackground { get; private set; }

        private static IntPtr hookPtr = IntPtr.Zero;
        private static HashSet<RawKey> pressedKeys = new HashSet<RawKey>();

        /// <summary>
        /// Initializes the service and starts processing input messages.
        /// </summary>
        /// <param name="workInBackround">Whether input messages should be handled when the application is not in focus.</param>
        /// <returns>Whether the service started successfully.</returns>
        public static bool Start (bool workInBackround)
        {
            if (IsRunning) return false;
            WorkInBackground = workInBackround;
            return SetHook();
        }

        /// <summary>
        /// Terminates the service and stops processing input messages.
        /// </summary>
        public static void Stop ()
        {
            RemoveHook();
            pressedKeys.Clear();
        }

        /// <summary>
        /// Checks whether provided key is currently pressed.
        /// </summary>
        public static bool IsKeyDown (RawKey key)
        {
            return pressedKeys.Contains(key);
        }

        private static bool SetHook ()
        {
            if (hookPtr == IntPtr.Zero)
            {
                if (WorkInBackground) hookPtr = Win32API.SetWindowsHookEx(HookType.WH_KEYBOARD_LL, HandleLowLevelHookProc, IntPtr.Zero, 0);
                else hookPtr = Win32API.SetWindowsHookEx(HookType.WH_KEYBOARD, HandleHookProc, IntPtr.Zero, (int)Win32API.GetCurrentThreadId());
            }

            if (hookPtr == IntPtr.Zero) return false;

            return true;
        }

        private static void RemoveHook ()
        {
            if (hookPtr != IntPtr.Zero)
            {
                Win32API.UnhookWindowsHookEx(hookPtr);
                hookPtr = IntPtr.Zero;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(Win32API.HookProc))]
        private static int HandleHookProc (int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0) 
                return Win32API.CallNextHookEx(hookPtr, code, wParam, lParam);

            var isKeyDown = ((int)lParam & (1 << 31)) == 0;
            var key = (RawKey)wParam;
            var handled = isKeyDown ? HandleKeyDown(key) : HandleKeyUp(key);

            return handled ? 1 : Win32API.CallNextHookEx(hookPtr, 0, wParam, lParam);
        }

        [AOT.MonoPInvokeCallback(typeof(Win32API.HookProc))]
        private static int HandleLowLevelHookProc (int code, IntPtr wParam, IntPtr lParam)
        {
            if (code < 0) 
                return Win32API.CallNextHookEx(hookPtr, code, wParam, lParam);

            var kbd = KBDLLHOOKSTRUCT.CreateFromPtr(lParam);
            var keyState = (RawKeyState)wParam;
            var key = (RawKey)kbd.vkCode;
            var isKeyDown = keyState == RawKeyState.KeyDown || keyState == RawKeyState.SysKeyDown;
            var handled = isKeyDown ? HandleKeyDown(key) : HandleKeyUp(key);

            return handled ? 1 : Win32API.CallNextHookEx(hookPtr, 0, wParam, lParam);
        }

        private static bool HandleKeyDown (RawKey key)
        {
            try
            {
                var added = pressedKeys.Add(key);
                return added && OnKeyDown != null && OnKeyDown.Invoke(key);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }

            return false;
        }

        private static bool HandleKeyUp (RawKey key)
        {
            try
            {
                var removed = pressedKeys.Remove(key);
                return removed && OnKeyUp != null && OnKeyUp.Invoke(key);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            
            return false;
        }
    }
}
