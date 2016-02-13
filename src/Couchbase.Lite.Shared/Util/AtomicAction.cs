//
//  AtomicAction.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Couchbase.Lite.Util
{
    public sealed class AtomicAction : IAtomicAction
    {
        #region Constants

        private const string TAG = "AtomicAction";
        private static readonly Action NULL_ACTION = () => {};

        #endregion

        #region Variables

        private List<Action> _performs = new List<Action>();
        private List<Action> _backOuts = new List<Action>();
        private List<Action> _cleanUps = new List<Action>();
        private int _nextStep;

        #endregion

        #region Properties

        public int FailedStep { get; private set; }

        #endregion

        #region Constructors

        public AtomicAction()
        {
            FailedStep = -1;
        }

        public AtomicAction(Action perform, Action backOut, Action cleanUp)
            : this()
        {
            AddLogic(perform, backOut, cleanUp);
        }

        #endregion

        #region Public Methods

        public static AtomicAction MoveDirectory(string srcPath, string dstPath)
        {
            var seq = new AtomicAction();
            seq.AddLogic(AtomicAction.DeleteDirectory(dstPath));
            seq.AddLogic(AtomicAction.MoveDirectoryUnsafe(srcPath, dstPath));
            return seq;
        }

        public static AtomicAction MoveDirectoryUnsafe(string srcPath, string dstPath)
        {
            if (srcPath == null || dstPath == null) {
                Log.W(TAG, "srcPath or dstPath in MoveDirectoryUnsafe is null");
                return null;
            }

            return new AtomicAction(() => Directory.Move(srcPath, dstPath), () => Directory.Move(dstPath, srcPath), null);
        }

        public static AtomicAction DeleteDirectory(string path)
        {
            if (path == null) {
                Log.W(TAG, "path in DeleteDirectory is null");
                return null;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), Misc.CreateGUID());
            var exists = false;
            return new AtomicAction(() =>
            {
                exists = Directory.Exists(path);
                if (!exists) {
                    return;
                }

                Directory.Move(path, tempPath);
            }, () =>
            {
                if (!exists) {
                    return;
                }

                Directory.Move(tempPath, path);
            }, () =>
            {
                if (!exists) {
                    return;
                }

                Directory.Delete(tempPath, true);
            });
        }

        public static AtomicAction MoveFile(string srcPath, string dstPath)
        {
            var seq = new AtomicAction();
            seq.AddLogic(AtomicAction.DeleteFile(dstPath));
            seq.AddLogic(AtomicAction.MoveFileUnsafe(srcPath, dstPath));
            return seq;
        }

        public static AtomicAction MoveFileUnsafe(string srcPath, string dstPath)
        {
            if (srcPath == null || dstPath == null) {
                Log.W(TAG, "srcPath or dstPath in MoveFileUnsafe is null");
                return null;
            }

            return new AtomicAction(() => File.Move(srcPath, dstPath), () => File.Move(dstPath, srcPath), null);
        }

        public static AtomicAction DeleteFile(string path)
        {
            if (path == null) {
                Log.W(TAG, "path in DeleteFile is null");
                return null;
            }

            var tempPath = Path.Combine(Path.GetTempPath(), Misc.CreateGUID());
            var exists = false;
            return new AtomicAction(() =>
            {
                exists = File.Exists(path);
                if (!exists) {
                    return;
                }

                File.Move(path, tempPath);
            }, () =>
            {
                if (!exists) {
                    return;
                }

                File.Move(tempPath, path);
            }, () =>
            {
                if (!exists) {
                    return;
                }

                File.Delete(tempPath);
            });
        }

        /// <summary>
        /// Adds an IAtomicAction as a step of this one.
        /// </summary>
        /// <param name="action">The action to add.</param>
        public void AddLogic(IAtomicAction action)
        {
            var cast = action as AtomicAction;
            if (cast != null) {
                _performs.AddRange(cast._performs);
                _backOuts.AddRange(cast._backOuts);
                _cleanUps.AddRange(cast._cleanUps);
            } else {
                AddLogic(action.Perform, action.BackOut, action.CleanUp);
            }
        }

        /// <summary>
        /// Adds an action as a step of this one. The action has three components, each optional.
        /// </summary>
        /// <param name="perform">A block that tries to perform the action, or throws an exception if it fails.
        /// (If the block fails, it should clean up; the backOut will _not_ be called!)</param>
        /// <param name="backOut">A block that undoes the effect of the action; it will be called if a _later_
        /// action fails, so that the system can be returned to the initial state.</param>
        /// <param name="cleanUp">A block that performs any necessary cleanup after all actions have been
        /// performed (e.g. deleting a temporary file.)</param>
        public void AddLogic(Action perform, Action backOut, Action cleanUp)
        {
            _performs.Add(perform ?? NULL_ACTION);
            _backOuts.Add(backOut ?? NULL_ACTION);
            _cleanUps.Add(cleanUp ?? NULL_ACTION);
        }

        public void AddLogic(Action perform, Action backoutOrCleanup)
        {
            AddLogic(perform, backoutOrCleanup, backoutOrCleanup);
        }

        /// <summary>
        /// Performs all the actions in order.
        /// If any action fails, backs out the previously performed actions in reverse order.
        /// If the actions succeeded, cleans them up in reverse order.
        /// The `FailedStep` property is set to the index of the failed perform block.
        /// </summary>
        public void Run()
        {
            Perform();
            CleanUp();
        }

        #endregion

        #region IAtomicAction
        #pragma warning disable 1591

        public void Perform()
        {
            Debug.Assert(_nextStep == 0, "Actions have already been run");
            FailedStep = -1;
            for (; _nextStep < _performs.Count; _nextStep++) {
                try {
                    _performs[_nextStep]();
                } catch(Exception e) {
                    Log.W(TAG, "Error performing step #{0}: {1}", _nextStep, e);
                    FailedStep = _nextStep;
                    if (_nextStep > 0) {
                        BackOut(); // back out the steps that already completed
                    }

                    throw new CouchbaseLiteException(e, StatusCode.Exception);
                }
            }
        }

        public void BackOut()
        {
            Debug.Assert(_nextStep > 0, "Actions have not been run");
            while (_nextStep-- > 0) {
                try {
                    _backOuts[_nextStep]();
                } catch(Exception e) {
                    Log.W(TAG, "Error backing out step #{0}: {1}", _nextStep, e);
                    throw new CouchbaseLiteException(e, StatusCode.Exception);
                }
            }
        }

        public void CleanUp()
        {
            Debug.Assert(_nextStep == _performs.Count, "Actions did not all run");
            while (_nextStep-- > 0) {
                try {
                    _cleanUps[_nextStep]();
                } catch(Exception e) {
                    Log.W(TAG, "Error cleaning up step #{0}: {1}", _nextStep, e);
                    throw new CouchbaseLiteException(e, StatusCode.Exception);
                }
            }
        }

        #pragma warning restore 1591
        #endregion
    }
}

