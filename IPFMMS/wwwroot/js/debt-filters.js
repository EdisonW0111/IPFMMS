(() => {
 const form=document.querySelector('[data-debt-filters]');
 if(!form||!window.fetch||!window.AbortController)return;
 const resultId=form.dataset.results,error=document.querySelector('[data-debt-error]');let sequence=0,active,timer;
 function sync(url){const q=new URL(url,location.href).searchParams;for(const field of form.querySelectorAll('input:not([type=hidden]),select'))field.value=q.get(field.name)||(field.name==='status'?'all':'');const exact=form.elements.expenseId;if(exact&&!q.has('expenseId'))exact.remove();else if(exact)exact.value=q.get('expenseId');else if(q.has('expenseId')){const input=document.createElement('input');input.type='hidden';input.name='expenseId';input.value=q.get('expenseId');form.append(input);}}
 async function load(url,push=true){clearTimeout(timer);active?.abort();active=new AbortController();const request=++sequence;const old=document.getElementById(resultId);old.setAttribute('aria-busy','true');error.hidden=true;
 try{const response=await fetch(url,{signal:active.signal,cache:'no-store',credentials:'same-origin',headers:{'X-Requested-With':'XMLHttpRequest'}});if(!response.ok)throw Error();const html=await response.text();if(request!==sequence)return;const next=new DOMParser().parseFromString(html,'text/html').getElementById(resultId);if(!next){if(response.redirected){location.assign(response.url);return;}throw Error();}old.replaceWith(next);sync(url);if(push)history.pushState(null,'',url);}
 catch(e){if(e.name!=='AbortError'&&request===sequence)error.hidden=false;}
 finally{if(request===sequence)document.getElementById(resultId).removeAttribute('aria-busy');}}
 function submit(){if(!form.reportValidity())return;const url=new URL(form.action);url.search=new URLSearchParams(new FormData(form));load(url.href);}
 form.addEventListener('submit',e=>{e.preventDefault();submit();});for(const field of form.querySelectorAll('select,input[type=month]'))field.addEventListener('change',submit);
 form.elements.search?.addEventListener('input',()=>{clearTimeout(timer);++sequence;active?.abort();timer=setTimeout(submit,350);});
 document.addEventListener('click',e=>{const a=e.target.closest(`[data-debt-reset],#${resultId} nav a`);if(!a||e.button!==0||e.ctrlKey||e.metaKey||e.shiftKey||e.altKey)return;e.preventDefault();load(a.href);});
 window.addEventListener('popstate',()=>{sync(location.href);load(location.href,false);});form.querySelector('[data-filter-submit]').hidden=true;
})();
