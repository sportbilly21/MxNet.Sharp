﻿using System;

// ReSharper disable once CheckNamespace
namespace MxNet
{
    /// <summary>
    ///     A class which has a pointer of MXNet object which is necessary to be disposed. This is an abstract class.
    /// </summary>
    public abstract class DisposableMXNetObject : MXNetObject, IDisposable
    {
        #region Constructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="DisposableMXNetObject" /> class.
        /// </summary>
        /// <param name="isMutable">Specifies whether this object is mutable.</param>
        /// <param name="isEnabledDispose">Specifies whether this object is disposed when call <see cref="Dispose" />.</param>
        protected DisposableMXNetObject(bool isMutable = true, bool isEnabledDispose = true)
        {
            IsMutable = isMutable;
            IsEnableDispose = isEnabledDispose;
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Gets a value indicating whether this object is already disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether this object is disposed when call <see cref="Dispose" />.
        /// </summary>
        public bool IsEnableDispose { get; }

        /// <summary>
        ///     Gets a value indicating whether this object is mutable.
        /// </summary>
        public bool IsMutable { get; }

        #endregion

        #region Methods

        /// <summary>
        ///     If this object is disposed, then <see cref="ObjectDisposedException" /> is thrown.
        /// </summary>
        public void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        #region Overrides

        /// <summary>
        ///     Releases managed resources.
        /// </summary>
        protected virtual void DisposeManaged()
        {
        }

        /// <summary>
        ///     Releases unmanaged resources.
        /// </summary>
        protected virtual void DisposeUnmanaged()
        {
        }

        #endregion

        #endregion

        #region IDisposable Members

        /// <summary>
        ///     Releases all resources used by this <see cref="DisposableMXNetObject" />.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        /// <summary>
        ///     Releases all resources used by this <see cref="DisposableMXNetObject" />.
        /// </summary>
        /// <param name="disposing">Indicate value whether <see cref="IDisposable.Dispose" /> method was called.</param>
        private void Dispose(bool disposing)
        {
            if (IsDisposed) return;

            IsDisposed = true;

            if (disposing)
                if (IsEnableDispose)
                    DisposeManaged();

            if (IsEnableDispose)
                DisposeUnmanaged();

            NativePtr = IntPtr.Zero;
        }

        #endregion
    }
}