FROM --platform=linux/amd64 artifactory.dev.adskengineer.net/cloudos-community/amzl2023/dotnet-6.0-sdk:latest

USER root

RUN yum -y update && yum -y install git python3-pip && yum clean all

ENTRYPOINT [""]

USER ctr-user
