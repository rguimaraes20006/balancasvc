

sudo apt-get install -y systemd  

useradd -m dotnetuser -p P@ssw0rd

export DOTNET_ROOT=$(pwd)/.dotnet


echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> ~/.bashrc


mkdir .dotnet 

wget https://download.visualstudio.microsoft.com/download/pr/58ebc46e-68d7-44db-aaea-5f5cb66a1cb5/44d292c80c0e13c444e6d66d67ca213e/dotnet-sdk-6.0.404-linux-arm.tar.gz

tar zxf  dotnet-sdk-6.0.404-linux-arm.tar.gz  -C .dotnet


export PATH=$PATH:/root/.dotnet:root/.dotnet/tools


git clone https://github.com/rguimaraes20006/balancasvc.git

cd balancasvc

dotnet publish -o /root/balancasvc/publish

sudo apt-get install -y systemd  

sudo useradd -m dotnetuser -p P@ssw0rd

sudo apt-get install -y systemd

cp balancasvc.service /etc/systemd/system/balancasvc.service

systemctl daemon-reload 

systemctl enable balancasvc.service

systemctl start balancasvc.service

