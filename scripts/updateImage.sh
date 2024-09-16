version=2.0.1
echo $version

docker build -t artifactory.dev.adskengineer.net/dynamo/pythonnet-build:$version .
docker push artifactory.dev.adskengineer.net/dynamo/pythonnet-build:$version
