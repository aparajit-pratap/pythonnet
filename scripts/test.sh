export PYTHONNET_PYDLL=$(python3 -m find_libpython)

dotnet test --logger "html;verbosity=detailed;logfilename=embed_tests-output.html" -p:targetframeworks=net6.0 src/embed_tests/
retVal=$?

# python3 -m pytest --runtime coreclr
# retVal=$(($retVal || $?))

dotnet test --logger "html;verbosity=detailed;logfilename=python_tests_runner-output.html" -p:targetframeworks=net6.0 src/python_tests_runner/
retVal=$(($retVal || $?))

exit $retVal
