#include<bits/stdc++.h>
using namespace std;
string reverseWord(string &str)
{
    int n=str.size();
    int i=0,j=n-1;
    while(i<j)
    {
        swap(str[i],str[j]);
        i++;j--;
    }
    return str;
}
int main()
{

    string str="geek";
    reverseWord(str);
    cout<<str<<endl;
    reverse(str.begin(),str.end());
    cout<<str<<endl;

    return 0;
}
