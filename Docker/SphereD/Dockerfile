FROM microsoft/dotnet:2.1-sdk

RUN git clone https://github.com/majikandy/StratisBitcoinFullNode.git

VOLUME /root/.stratisbitcoin

WORKDIR /StratisBitcoinFullNode/src
RUN git checkout sphere
WORKDIR /StratisBitcoinFullNode/src/SphereD
RUN dotnet build

COPY sphere.conf.docker /root/.stratisnode/stratis/StratisMain/sphere.conf

EXPOSE 18444 18442 26174 26178 37220

CMD ["dotnet", "run"]
