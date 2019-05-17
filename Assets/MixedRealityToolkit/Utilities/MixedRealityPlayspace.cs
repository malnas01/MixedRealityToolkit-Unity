﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.MixedReality.Toolkit.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Microsoft.MixedReality.Toolkit
{
    /// <summary>
    /// A static class encapsulating the Mixed Reality playspace.
    /// </summary>
    public static class MixedRealityPlayspace
    {
        private const string Name = "MixedRealityPlayspace";

        private static Transform mixedRealityPlayspace;

        /// <summary>
        /// The transform of the playspace.
        /// </summary>
        public static Transform Transform
        {
            get
            {
                if (mixedRealityPlayspace)
                {
                    mixedRealityPlayspace.gameObject.SetActive(true);
                    return mixedRealityPlayspace;
                }

                if (CameraCache.Main.transform.parent == null)
                {
                    // Create a new mixed reality playspace
                    GameObject mixedRealityPlayspaceGo = new GameObject(Name);
                    mixedRealityPlayspace = mixedRealityPlayspaceGo.transform;
                    CameraCache.Main.transform.SetParent(mixedRealityPlayspace);
                }
                else
                {
                    if (CameraCache.Main.transform.parent.name != Name)
                    {
                        // Since the scene is set up with a different camera parent, its likely
                        // that there's an expectation that that parent is going to be used for
                        // something else. We print a warning to call out the fact that we're 
                        // co-opting this object for use with teleporting and such, since that
                        // might cause conflicts with the parent's intended purpose.
                        Debug.LogWarning($"The Mixed Reality Toolkit expected the camera\'s parent to be named {Name}. The existing parent will be renamed and used instead.");
                        // If we rename it, we make it clearer that why it's being teleported around at runtime.
                        CameraCache.Main.transform.parent.name = Name;
                    }

                    mixedRealityPlayspace = CameraCache.Main.transform.parent;
                }

                // It's very important that the Playspace align with the tracked space,
                // otherwise reality-locked things like playspace boundaries won't be aligned properly.
                // For now, we'll just assume that when the playspace is first initialized, the
                // tracked space origin overlaps with the world space origin. If a platform ever does
                // something else (i.e, placing the lower left hand corner of the tracked space at world 
                // space 0,0,0), we should compensate for that here.
                return mixedRealityPlayspace;
            }
        }

        /// <summary>
        /// The location of the playspace.
        /// </summary>
        public static Vector3 Position
        {
            get { return Transform.position; }
            set { Transform.position = value; }
        }

        /// <summary>
        /// The playspace's rotation.
        /// </summary>
        public static Quaternion Rotation
        {
            get { return Transform.rotation; }
            set { Transform.rotation = value;  }
        }

        /// <summary>
        /// Adds a child object to the playspace's heirarchy.
        /// </summary>
        /// <param name="transform">The child object's transform.</param>
        public static void AddChild(Transform transform)
        {
            transform.SetParent(Transform);
        }

        /// <summary>
        /// Transforms a position from local to world space.
        /// </summary>
        /// <param name="localPosition">The position to be transformed.</param>
        /// <returns>
        /// The position, in world space.
        /// </returns>
        public static Vector3 TransformPoint(Vector3 localPosition)
        {
            return Transform.TransformPoint(localPosition);
        }

        /// <summary>
        /// Transforms a position from world to local space.
        /// </summary>
        /// <param name="worldPosition">The position to be transformed.</param>
        /// <returns>
        /// The position, in local space.
        /// </returns>
        public static Vector3 InverseTransformPoint(Vector3 worldPosition)
        {
            return Transform.InverseTransformPoint(worldPosition);
        }

        /// <summary>
        /// Transforms a direction from local to world space.
        /// </summary>
        /// <param name="localDirection">The direction to be transformed.</param>
        /// <returns>
        /// The direction, in world space.
        /// </returns>
        public static Vector3 TransformDirection(Vector3 localDirection)
        {
            return Transform.TransformDirection(localDirection);
        }

        /// <summary>
        /// Rotates the playspace around the specified axis.
        /// </summary>
        /// <param name="point">The point to pass through during rotation.</param>
        /// <param name="axis">The axis about which to rotate.</param>
        /// <param name="angle">The angle, in degrees, to rotate.</param>
        public static void RotateAround(Vector3 point, Vector3 axis, float angle)
        {
            Transform.RotateAround(point, axis, angle);
        }

        /// <summary>
        /// Performs a playspace transformation.
        /// </summary>
        /// <param name="transformation">The transformation to be applied to the playspace.</param>
        /// <remarks>
        /// This method takes a lambda function and may contribute to garbage collector pressure.
        /// For best performance, avoid calling this method from an inner loop function.
        /// </remarks>
        public static void PerformTransformation(Action<Transform> transformation)
        {
            transformation?.Invoke(Transform);
        }
        
        #region Multi-scene management

        private static bool subscribedToEvents = false;
        
#if UNITY_EDITOR
        private static bool subscribedToEditorEvents = false;

        [InitializeOnLoadMethod]
        public static void InitializeOnLoad()
        {
            Debug.Log("InitializeOnLoad");
            if (!subscribedToEditorEvents)
            {
                EditorSceneManager.sceneOpened += EditorSceneManagerSceneOpened;
                EditorSceneManager.sceneClosed += EditorSceneManagerSceneClosed;
                subscribedToEditorEvents = true;
            }

            SearchForAndEnableExistingPlayspace(EditorSceneUtils.GetRootGameObjectsInLoadedScenes());
        }

        private static void EditorSceneManagerSceneClosed(Scene scene)
        {
            if (Application.isPlaying)
            {   // Let the runtime scene management handle this
                return;
            }

            Debug.Log("EditorSceneManagerSceneClosed");

            if (mixedRealityPlayspace == null)
            {   // If we unloaded our playspace, see if another one exists
                SearchForAndEnableExistingPlayspace(EditorSceneUtils.GetRootGameObjectsInLoadedScenes());
            }
        }

        private static void EditorSceneManagerSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (Application.isPlaying)
            {   // Let the runtime scene management handle this
                return;
            }

            Debug.Log("EditorSceneManagerSceneOpened");

            if (mixedRealityPlayspace == null)
            {
                SearchForAndEnableExistingPlayspace(EditorSceneUtils.GetRootGameObjectsInLoadedScenes());
            }
            else
            {
                SearchForAndDisableExtraPlayspaces(scene.GetRootGameObjects());
            }
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RuntimeInitializeOnLoadMethod()
        {
            Debug.Log("RuntimeInitializeOnLoadMethod");
            if (!subscribedToEvents)
            {
                SceneManager.sceneLoaded += SceneManagerSceneLoaded;
                SceneManager.sceneUnloaded += SceneManagerSceneUnloaded;
                subscribedToEvents = true;
            }
        }

        private static void SceneManagerSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            Debug.Log("SceneManager scene loaded");
            if (mixedRealityPlayspace == null)
            {
                SearchForAndEnableExistingPlayspace(RuntimeSceneUtils.GetRootGameObjectsInLoadedScenes());
            }
            else
            {
                SearchForAndDisableExtraPlayspaces(scene.GetRootGameObjects());
            }
        }

        private static void SceneManagerSceneUnloaded(Scene scene)
        {
            if (mixedRealityPlayspace == null)
            {   // If we unloaded our playspace, see if another one exists
                SearchForAndEnableExistingPlayspace(RuntimeSceneUtils.GetRootGameObjectsInLoadedScenes());
            }
        }

        private static void SearchForAndDisableExtraPlayspaces(IEnumerable<GameObject> rootGameObjects)
        {
            // We've already got a mixed reality playspace.
            // Our task is to search for any additional play spaces that may have been loaded, and disable them.
            foreach (GameObject rootGameObject in rootGameObjects)
            {
                if (rootGameObject.name.Equals(Name))
                {
                    rootGameObject.SetActive(false);
                }
            }
        }

        private static void SearchForAndEnableExistingPlayspace(IEnumerable<GameObject> rootGameObjects)
        {
            // We haven't created / found a playspace yet.
            // Our task is to see if one exists in the newly loaded scene.
            bool enabledOne = false;
            foreach (GameObject rootGameObject in rootGameObjects)
            {
                if (rootGameObject.name.Equals(Name))
                {
                    if (!enabledOne)
                    {
                        Debug.Log("Found existing " + Name + " in newly loaded scene.");
                        mixedRealityPlayspace = rootGameObject.transform;
                        mixedRealityPlayspace.gameObject.SetActive(true);
                        enabledOne = true;
                    }
                    else
                    {   // If we've already enabled one, we need to disable all others
                        rootGameObject.SetActive(false);
                    }
                    return;
                }
            }
        }

        #endregion
    }
}