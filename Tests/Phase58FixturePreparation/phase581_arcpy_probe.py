"""Phase 5.8.1 read-only ArcGIS Python execution-context probes.

This script deliberately performs no FileGDB creation, dataset mutation, or
ArcGISProject operation.  Each mode is run in a separate process by the
recovery-gate harness.
"""

from __future__ import print_function

import os
import sys


def emit(label, value):
    print("{}={}".format(label, value), flush=True)


def identity():
    emit("PROBE_PID", os.getpid())
    emit("PROBE_PPID", os.getppid())
    emit("PYTHON_EXECUTABLE", sys.executable)
    emit("PYTHON_VERSION", sys.version.replace("\n", " "))
    emit("PYTHON_PREFIX", sys.prefix)
    emit("PYTHON_BASE_PREFIX", sys.base_prefix)
    emit("CWD", os.getcwd())
    emit("SYSPATH_0", sys.path[0] if sys.path else "")
    emit("SYSPATH_SUMMARY", "|".join(sys.path[:8]))


def environment_audit():
    """Emit only the Recovery Gate environment whitelist, never a full env dump."""
    for name in ("PYTHONHOME", "PYTHONPATH", "CONDA_PREFIX", "CONDA_DEFAULT_ENV", "TEMP", "TMP"):
        value = os.environ.get(name)
        if value is None:
            emit("ENV_{}".format(name), "<unset>")
        elif name in ("CONDA_PREFIX", "CONDA_DEFAULT_ENV", "TEMP", "TMP"):
            emit("ENV_{}".format(name), value)
        else:
            emit("ENV_{}".format(name), "<set:length={}>".format(len(value)))

    path_entries = os.environ.get("PATH", "").split(os.pathsep)
    arcgis_entries = [
        entry
        for entry in path_entries
        if "\\arcgis\\pro\\" in entry.lower()
    ]
    emit("ENV_PATH_LENGTH", len(os.environ.get("PATH", "")))
    emit("ENV_PATH_ARCGIS_ENTRIES", "|".join(arcgis_entries) if arcgis_entries else "<none>")
    emit("ENV_PATH_HAS_PRO_BIN", any(entry.lower().rstrip("\\") == r"c:\program files\arcgis\pro\bin" for entry in path_entries))
    emit("ENV_PATH_HAS_PRO_PYTHON_BIN", any("\\arcgis\\pro\\bin\\python" in entry.lower() for entry in path_entries))


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "identity"
    emit("PROBE_MODE", mode)
    identity()
    emit("PROBE_STAGE", "identity_complete")

    if mode == "core":
        emit("CORE_OK", "true")
        return

    if mode == "stdlib":
        import json
        import pathlib
        import ctypes

        emit("STDLIB_IMPORTS", "json,pathlib,ctypes")
        emit("STDLIB_OK", "true")
        return

    if mode == "numpy":
        import numpy

        emit("NUMPY_VERSION", numpy.__version__)
        emit("NUMPY_OK", "true")
        return

    if mode == "env_audit":
        environment_audit()
        emit("ENV_AUDIT_OK", "true")
        return

    if mode == "arcgisscripting":
        import arcgisscripting

        emit("ARCGISSCRIPTING_MODULE", getattr(arcgisscripting, "__file__", ""))
        emit("ARCGISSCRIPTING_OK", "true")
        return

    if mode == "arcpy":
        import arcpy

        emit("ARCPY_OK", "true")
        emit("ARCPY_INSTALL_INFO", arcpy.GetInstallInfo())
        return

    if mode == "arcpy_no_imports":
        os.environ["ARCPY_NO_IMPORTS"] = "1"
        emit("ARCPY_NO_IMPORTS", os.environ["ARCPY_NO_IMPORTS"])
        import arcpy

        emit("ARCPY_OK_NO_IMPORTS", "true")
        emit("ARCPY_INSTALL_INFO", arcpy.GetInstallInfo())
        return

    raise SystemExit("unknown probe mode: {}".format(mode))


if __name__ == "__main__":
    main()
